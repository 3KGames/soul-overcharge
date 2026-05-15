using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class IndicatorManager : MonoBehaviour
{
    [SerializeField] private GameObject indicatorPrefab;
    [SerializeField] private float padding = 50f;

    [Header("Sprites")]
    [SerializeField] private Sprite closeSprite;
    [SerializeField] private Sprite mediumSprite;
    [SerializeField] private Sprite farSprite;

    [Header("Distance")]
    [SerializeField] private float closeThreshold = 10f;
    [SerializeField] private float mediumThreshold = 25f;
	[SerializeField] private float farThreshold = 25f;

    private TargetRegistry _registry;
    private Camera _mainCamera;
    
    private class IndicatorData
    {
        public RectTransform Rect;
        public Image Img;
    }

    private Dictionary<Transform, IndicatorData> _indicators = new Dictionary<Transform, IndicatorData>();

    [Inject]
    public void Construct(TargetRegistry registry)
    {
        _registry = registry;
    }

    private void Start()
    {
        _mainCamera = Camera.main;

        _registry.OnTargetAdded += AddIndicator;
        _registry.OnTargetRemoved += RemoveIndicator;

        foreach (var target in _registry.ActiveTargets)
        {
            AddIndicator(target);
        }
    }

    private void OnDestroy()
    {
        if (_registry != null)
        {
            _registry.OnTargetAdded -= AddIndicator;
            _registry.OnTargetRemoved -= RemoveIndicator;
        }
    }

    private void AddIndicator(Transform target)
    {
        var instance = Instantiate(indicatorPrefab, transform);
        
        var data = new IndicatorData
        {
            Rect = instance.GetComponent<RectTransform>(),
            Img = instance.GetComponent<Image>()
        };
        
        _indicators.Add(target, data);
    }

    private void RemoveIndicator(Transform target)
    {
        if (_indicators.TryGetValue(target, out var data))
        {
            Destroy(data.Rect.gameObject);
            _indicators.Remove(target);
        }
    }

    private void LateUpdate()
    {
        foreach (var kvp in _indicators)
        {
            Transform target = kvp.Key;
            IndicatorData indicatorData = kvp.Value;

            UpdateIndicator(target, indicatorData);
        }
    }

    private void UpdateIndicator(Transform target, IndicatorData data)
    {
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(target.position);
        
        bool isBehind = screenPos.z < 0;
        bool isOffScreen = isBehind || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height;
		float distance = Vector3.Distance(_mainCamera.transform.position, target.position);

        if (isOffScreen && distance < farThreshold)
        {
            data.Rect.gameObject.SetActive(true);

            if (distance < closeThreshold) data.Img.sprite = closeSprite;
            else if (distance < mediumThreshold) data.Img.sprite = mediumSprite;
            else data.Img.sprite = farSprite;

            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            
            Vector3 dir = screenPos - screenCenter;
            
            if (isBehind) 
            {
                dir *= -1;
            }
            
            dir.z = 0;
            dir = dir.normalized;

            float boundsX = (Screen.width / 2f) - padding;
            float boundsY = (Screen.height / 2f) - padding;

            float m = Mathf.Min(
                boundsX / Mathf.Abs(dir.x), 
                boundsY / Mathf.Abs(dir.y)
            );

            Vector3 edgePosition = screenCenter + dir * m;
            
            data.Rect.position = edgePosition;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            data.Rect.rotation = Quaternion.Euler(0, 0, angle);
        }
        else
        {
            data.Rect.gameObject.SetActive(false);
        }
    }
}