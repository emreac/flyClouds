using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ControlBlur : MonoBehaviour
{
    public Volume globalVolume;
    private DepthOfField dof;
    private bool toggleDof;
    public float focusDistance;

    // Changed to public so SettingsMenuController can call it
    public void ToggleBGUI()
    {
        toggleDof = !toggleDof;
        if (globalVolume.profile.TryGet(out dof))
        {
            dof.active = toggleDof;
            dof.focusDistance.value = focusDistance;
        }
    }
}
