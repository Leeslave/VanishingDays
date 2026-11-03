using UnityEditor;

namespace VanishingDays.Editor
{
    public static class InterfaceRepairChecks
    {
        public static void Play()
        {
            OnboardingChecks.Validate();WaterAccessChecks.Validate();
            NicknameInputChecks.Play();
        }
    }
}
