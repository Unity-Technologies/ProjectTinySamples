using JetBrains.Annotations;
using System.Collections.Generic;
using System.Linq;
using Unity.Platforms;
using Unity.Properties.UI;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Entities.Runtime.Build
{
    [UsedImplicitly]
    sealed class BuildTargetInspector : Inspector<BuildTarget>
    {
        PopupField<BuildTarget> m_TargetPopup;

        public override VisualElement Build()
        {
            m_TargetPopup = new PopupField<BuildTarget>(GetAvailableTargets(), 0, GetDisplayName, GetDisplayName)
            {
                label = DisplayName
            };

            m_TargetPopup.RegisterValueChangedCallback(evt =>
            {
                Target = evt.newValue;
            });
            return m_TargetPopup;
        }

        public override void Update()
        {
            m_TargetPopup.SetValueWithoutNotify(Target);
        }

        static List<BuildTarget> GetAvailableTargets() => BuildTarget.AvailableBuildTargets.Where(target => !target.HideInBuildTargetPopup).ToList();
        static string GetDisplayName(BuildTarget target) => target.DisplayName;
    }
}
