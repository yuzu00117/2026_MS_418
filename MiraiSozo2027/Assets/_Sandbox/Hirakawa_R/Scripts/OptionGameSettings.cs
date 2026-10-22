using UnityEngine;

namespace MiraiSozo.Options
{
    public class OptionGameSettings : MonoBehaviour
    {
        public static OptionGameSettings Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [SerializeField] private bool _isFirstPersonMode = false;
        public bool IsFirstPersonMode => _isFirstPersonMode;

        public void ToggleFirstPersonMode()
        {
            _isFirstPersonMode = !_isFirstPersonMode;
        }   
        
    }
}
