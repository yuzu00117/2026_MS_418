using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

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
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
