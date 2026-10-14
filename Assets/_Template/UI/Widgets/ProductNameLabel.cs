using TMPro;
using UnityEngine;

namespace Template.UI.Widgets
{
    /// <summary>Shows Player Settings > Product Name, so a cloned game needs no UI text edits for its title.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ProductNameLabel : MonoBehaviour
    {
        private void Awake() => GetComponent<TMP_Text>().text = Application.productName.ToUpperInvariant();
    }
}
