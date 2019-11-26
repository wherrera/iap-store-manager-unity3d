# iap-store-manager-unity3d
IAP Store Manager for Unity3D in C#

## Example Store button

```csharp
[RequireComponent(typeof(Button))]
public class PurchaseStoreItemButton : MonoBehaviour
{
    public StoreItem storeItem;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    void OnClick()
    {
#if UNITY_PURCHASING
      StoreManager.Instance.BuyProductID(storeItem.id);
#endif
		}
}
```



## Example Store Controller

```csharp
    public class StoreController : MonoBehaviour
    {
        public UnityEvent OnPurchasePremiumEvent;
        public UnityEvent OnPurchaseFailedEvent;

        private void Awake()
        {
#if UNITY_PURCHASING
            StoreManager.Instance.OnStorePurchase.AddListener(OnStorePurchaseEvent);
#endif
		}

#if UNITY_PURCHASING
        public void OnStorePurchaseEvent(bool success, Product product)
        {
            if (success)
            {
                if (string.Equals("store.item.id", product.definition.id))
                {
                    if (OnPurchasePremiumEvent != null)
                        OnPurchasePremiumEvent.Invoke();
                }
            }
            else
            {
                if (OnPurchaseFailedEvent != null)
                    OnPurchaseFailedEvent.Invoke();
            }
        }
#endif
	}
```

