using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Aura.Inspector.Editor
{
    /// @brief [FilteredEventMenu] を付けた UnityEvent を、Unity のふつうの描き方（UnityEventDrawer）で描く
    /// 関数のメニューだけは、[ShowInEventMenu] の印があるスクリプトから、印の付いていないメンバー（MonoBehaviour から受け継いだ
    /// enabled や SendMessage など）を取り除いて出す。印の無いスクリプト（Transform、Light など）は、ふつうのメニューのまま
    /// メニューを作る Unity の処理（UnityEventDrawer.BuildPopupList）は公開されていないので、名前で探して呼ぶ（Unity 6000.6 で確かめた）
    /// Unity のバージョンが変わって見つからないときは、取り除かずに、ふつうのメニューを出す（壊れない）
    [CustomPropertyDrawer(typeof(FilteredEventMenuAttribute))]
    public sealed class FilteredEventMenuDrawer : UnityEventDrawer
    {
        // 関数のボタンの位置。UnityEventDrawer.GetRowRects と同じ計算（行の上から 2、高さ 18、左 30% と間 5 の右から右端まで）
        private const float RowTopOffset = 2.0f;
        private const float RowLineHeight = 18.0f;
        private const float TargetColumnRatio = 0.3f;
        private const float ColumnSpacing = 5.0f;

        private const string CallsPath = "m_PersistentCalls.m_Calls";
        private const string TargetPath = "m_Target";
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags PublicMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

        private static readonly MethodInfo BuildPopupListMethod = typeof(UnityEventDrawer).GetMethod(
            "BuildPopupList", AnyStatic, null, new[] { typeof(Object), typeof(UnityEventBase), typeof(SerializedProperty) }, null);
        private static readonly FieldInfo DummyEventField = typeof(UnityEventDrawer).GetField("m_DummyEvent", AnyInstance);
        private static readonly PropertyInfo MenuItemsProperty = typeof(GenericMenu).GetProperty("menuItems", AnyInstance);
        private static readonly Dictionary<Type, bool> HasMarksByType = new Dictionary<Type, bool>();  ///< 型ごとの「印があるか」を覚えておく

        private SerializedProperty _eventProperty;  ///< 今描いている UnityEvent。DrawEvent で、その行の項目を引くために持つ

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            _eventProperty = property;
            base.OnGUI(position, property, label);
        }

        // UI Toolkit の描き方（メニューを差し替えられない）を使わず、IMGUI の OnGUI で描かせる
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return null;
        }

        protected override void DrawEvent(Rect rect, int index, bool isActive, bool isFocused)
        {
            // 関数のボタンが押されたときだけ、ふつうの描き方より先に、絞ったメニューを出す（押したことは使い終わりにして、ふつうのメニューは出さない）
            TryShowFilteredMenu(rect, index);
            base.DrawEvent(rect, index, isActive, isFocused);
        }

        private void TryShowFilteredMenu(Rect rect, int index)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || _eventProperty == null)
            {
                return;
            }
            var functionRect = new Rect(rect.x + rect.width * TargetColumnRatio + ColumnSpacing, rect.y + RowTopOffset,
                rect.width * (1.0f - TargetColumnRatio) - ColumnSpacing, RowLineHeight);
            if (!functionRect.Contains(current.mousePosition))
            {
                return;
            }

            SerializedProperty calls = _eventProperty.FindPropertyRelative(CallsPath);
            if (calls == null || index < 0 || index >= calls.arraySize)
            {
                return;
            }
            SerializedProperty listener = calls.GetArrayElementAtIndex(index);
            Object target = listener.FindPropertyRelative(TargetPath).objectReferenceValue;
            if (target == null || !HasMarkedComponent(target))
            {
                return;
            }

            GenericMenu menu = BuildFilteredMenu(target, listener);
            if (menu == null)
            {
                return;
            }
            current.Use();
            menu.DropDown(functionRect);
        }

        /// @brief Unity と同じメニューを作り、印のあるスクリプトの、印の無い行を取り除く
        /// @return 作ったメニュー。Unity の内部の処理が見つからないときは null（ふつうのメニューに任せる）
        private GenericMenu BuildFilteredMenu(Object target, SerializedProperty listener)
        {
            if (BuildPopupListMethod == null || DummyEventField == null || MenuItemsProperty == null)
            {
                return null;
            }
            var dummyEvent = DummyEventField.GetValue(this) as UnityEventBase;
            if (dummyEvent == null)
            {
                return null;
            }

            try
            {
                var menu = (GenericMenu)BuildPopupListMethod.Invoke(null, new object[] { target, dummyEvent, listener });
                if (!(MenuItemsProperty.GetValue(menu) is IList items))
                {
                    return null;
                }
                for (int i = items.Count - 1; i >= 0; --i)
                {
                    if (TryGetFunction(items[i], out Object itemTarget, out MethodInfo method)
                        && itemTarget != null && HasMarks(itemTarget.GetType()) && !IsMarked(method))
                    {
                        items.RemoveAt(i);
                    }
                }
                return menu;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("FilteredEventMenuDrawer: Unity の内部の処理が変わったので、メニューを絞らずに出す\n" + exception);
                return null;
            }
        }

        /// @brief つなぐ先（GameObject か、その部品）に、印のあるスクリプトがあるか
        private static bool HasMarkedComponent(Object target)
        {
            GameObject gameObject = target as GameObject;
            if (gameObject == null && target is Component component)
            {
                gameObject = component.gameObject;
            }
            if (gameObject == null)
            {
                return HasMarks(target.GetType());
            }
            foreach (Component each in gameObject.GetComponents<Component>())
            {
                if (each != null && HasMarks(each.GetType()))
                {
                    return true;
                }
            }
            return false;
        }

        /// @brief メニューの 1 行から、つなぐ物と呼ぶメソッドを取り出す（行に入っている UnityEventDrawer.UnityEventFunction から）
        /// @return 呼ぶメソッドがある行なら true（「No Function」や見出しの行は false）
        private static bool TryGetFunction(object menuItem, out Object outTarget, out MethodInfo outMethod)
        {
            outTarget = null;
            outMethod = null;
            object userData = menuItem?.GetType().GetField("userData", AnyInstance)?.GetValue(menuItem);
            if (userData == null)
            {
                return false;
            }
            Type functionType = userData.GetType();
            outTarget = functionType.GetField("m_Target", AnyInstance)?.GetValue(userData) as Object;
            outMethod = functionType.GetField("m_Method", AnyInstance)?.GetValue(userData) as MethodInfo;
            return outMethod != null;
        }

        /// @brief その型に、[ShowInEventMenu] の付いた public なメソッドかプロパティがあるか
        private static bool HasMarks(Type type)
        {
            if (HasMarksByType.TryGetValue(type, out bool hasMarks))
            {
                return hasMarks;
            }
            hasMarks = false;
            foreach (MethodInfo method in type.GetMethods(PublicMembers))
            {
                hasMarks |= method.IsDefined(typeof(ShowInEventMenuAttribute), true);
            }
            foreach (PropertyInfo property in type.GetProperties(PublicMembers))
            {
                hasMarks |= property.IsDefined(typeof(ShowInEventMenuAttribute), true);
            }
            HasMarksByType[type] = hasMarks;
            return hasMarks;
        }

        /// @brief そのメソッド（プロパティなら、値を入れる set のメソッド）に [ShowInEventMenu] が付いているか
        private static bool IsMarked(MethodInfo method)
        {
            if (method.IsDefined(typeof(ShowInEventMenuAttribute), true))
            {
                return true;
            }
            if (!method.IsSpecialName || method.DeclaringType == null)
            {
                return false;
            }
            foreach (PropertyInfo property in method.DeclaringType.GetProperties(PublicMembers))
            {
                MethodInfo setter = property.GetSetMethod();
                if (setter != null && setter.MethodHandle == method.MethodHandle)
                {
                    return property.IsDefined(typeof(ShowInEventMenuAttribute), true);
                }
            }
            return false;
        }
    }
}
