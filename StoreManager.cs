/*
MIT License

Copyright (c) 2019 William Herrera

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_PURCHASING
using UnityEngine.Purchasing;
#endif

namespace StoreManager
{
    public enum ItemType
    {
        Consumable,
        NonConsumable,
        Subscription
    }

    [CreateAssetMenu(fileName = "new store item definition", menuName = "StoreItem")]
    public class StoreItem : ScriptableObject
    {
        public string id;
        public ItemType type;

        [Header("Appstore specific Id's")]
        public string appleAppStore;
        public string macAppStore;
        public string googlePlayStore;
        public string amazonAppStore;

#if UNITY_PURCHASING
        public ProductType GetProductType()
        {
            if (type == ItemType.Consumable) return ProductType.Consumable;
            else if (type == ItemType.NonConsumable) return ProductType.NonConsumable;
            else return ProductType.Subscription;
        }
#endif
    }

    [Serializable]
    public class StoreInitializedEvent : UnityEvent<bool> { }

    [Serializable]
    public class StorePurchaseEvent : UnityEvent<bool, Product> { }    

    public class StoreManager : MonoBehaviour
    {
        private static StoreManager m_Instance;

#if UNITY_PURCHASING
        private static IStoreController m_StoreController;          // The Unity Purchasing system.
        public static IExtensionProvider m_StoreExtensionProvider; // The store-specific Purchasing subsystems.
        private StoreListener m_StoreListener;

        private class StoreListener : IStoreListener
        {
            public StoreManager storeManager;

            public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
            {
                // Purchasing has succeeded initializing. Collect our Purchasing references.
                Debug.Log("OnInitialized: PASS");

                // Overall Purchasing system, configured with products for this application.
                m_StoreController = controller;
                // Store specific subsystem, for accessing device-specific store features.
                m_StoreExtensionProvider = extensions;

                if (storeManager.OnStoreInitialized != null)
                    storeManager.OnStoreInitialized.Invoke(true);
            }


            public void OnInitializeFailed(InitializationFailureReason error)
            {
                // Purchasing set-up has not succeeded. Check error for reason. Consider sharing this reason with the user.
                Debug.Log("OnInitializeFailed InitializationFailureReason:" + error);

                if (storeManager.OnStoreInitialized != null)
                    storeManager.OnStoreInitialized.Invoke(false);
            }


            public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
            {
                if (storeManager.OnStorePurchase != null)
                    storeManager.OnStorePurchase.Invoke(true, args.purchasedProduct);

                return PurchaseProcessingResult.Complete;
            }


            public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
            {
                if (storeManager.OnStorePurchase != null)
                    storeManager.OnStorePurchase.Invoke(false, product);

                Debug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}", product.definition.storeSpecificId, failureReason));
            }
        }
#endif

        public StoreInitializedEvent OnStoreInitialized;
        public StorePurchaseEvent OnStorePurchase;

        public StoreItem[] products;

        public static StoreManager Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = FindObjectOfType<StoreManager>();
                }
                return m_Instance;
            }            
        }

        void Awake()
        {
            DontDestroyOnLoad(this);
        }

        public void InitializePurchasing()
        {
            if (IsInitialized())
            {
                return;
            }
#if UNITY_PURCHASING

            m_StoreListener = new StoreListener();

            m_StoreListener.storeManager = this;

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

            foreach (StoreItem item in products)
            {
                builder.AddProduct(item.id, item.GetProductType(), new IDs(){
                    { item.appleAppStore, AppleAppStore.Name },
                    { item.googlePlayStore, GooglePlay.Name },
                    { item.macAppStore, MacAppStore.Name},
                    { item.amazonAppStore, AmazonApps.Name},
                });
            }

            UnityPurchasing.Initialize(m_StoreListener, builder);
#endif
        }


        private bool IsInitialized()
        {
#if UNITY_PURCHASING
            return m_StoreListener != null && m_StoreController != null && m_StoreExtensionProvider != null;
#else
            return false;
#endif
        }

        public void BuyProductID(string productId)
        {
#if UNITY_PURCHASING
            // If Purchasing has been initialized ...
            if (IsInitialized())
            {
                // ... look up the Product reference with the general product identifier and the Purchasing 
                // system's products collection.
                Product product = m_StoreController.products.WithID(productId);

                // If the look up found a product for this device's store and that product is ready to be sold ... 
                if (product != null && product.availableToPurchase)
                {
                    Debug.Log(string.Format("Purchasing product asychronously: '{0}'", product.definition.id));
                    // ... buy the product. Expect a response either through ProcessPurchase or OnPurchaseFailed 
                    // asynchronously.
                    m_StoreController.InitiatePurchase(product);
                }
                // Otherwise ...
                else
                {
                    if (OnStorePurchase != null)
                        OnStorePurchase.Invoke(false, null);
                    
                    // ... report the product look-up failure situation  
                    Debug.Log("BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase");
                }
            }
            // Otherwise ...
            else
            {
                if (OnStorePurchase != null)
                    OnStorePurchase.Invoke(false, null);
                
                // ... report the fact Purchasing has not succeeded initializing yet. Consider waiting longer or 
                // retrying initiailization.
                Debug.Log("BuyProductID FAIL. Not initialized.");
            }
#endif
        }


        // Restore purchases previously made by this customer. Some platforms automatically restore purchases, like Google. 
        // Apple currently requires explicit purchase restoration for IAP, conditionally displaying a password prompt.
        public void RestorePurchases()
        {
#if UNITY_PURCHASING
            // If Purchasing has not yet been set up ...
            if (!IsInitialized())
            {
                // ... report the situation and stop restoring. Consider either waiting longer, or retrying initialization.
                Debug.Log("RestorePurchases FAIL. Not initialized.");
                return;
            }

            // If we are running on an Apple device ... 
            if (Application.platform == RuntimePlatform.IPhonePlayer ||
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                // ... begin restoring purchases
                Debug.Log("RestorePurchases started ...");

                // Fetch the Apple store-specific subsystem.
                var apple = m_StoreExtensionProvider.GetExtension<IAppleExtensions>();
                // Begin the asynchronous process of restoring purchases. Expect a confirmation response in 
                // the Action<bool> below, and ProcessPurchase if there are previously purchased products to restore.
                apple.RestoreTransactions((result) => {
                    // The first phase of restoration. If no more responses are received on ProcessPurchase then 
                    // no purchases are available to be restored.
                    Debug.Log("RestorePurchases continuing: " + result + ". If no further messages, no purchases available to restore.");
                });
            }
            // Otherwise ...
            else
            {
                // We are not running on an Apple device. No work is necessary to restore purchases.
                Debug.Log("RestorePurchases FAIL. Not supported on this platform. Current = " + Application.platform);
            }
#endif
        }
    }
}