using UnityEngine;

public sealed class RadioButtonInteractable : MonoBehaviour
{
    public enum ButtonAction
    {
        TogglePlayPause,
        PreviousTrack,
        NextTrack
    }

    [Tooltip("Bu tiklama alaninin radyoda calistiracagi islem.")]
    public ButtonAction action;

    [Tooltip("Kontrol edilecek radyo. Bos birakilirsa ust objelerde aranir.")]
    public RadioMusicController radio;

    public void Activate()
    {
        if (radio == null)
            radio = GetComponentInParent<RadioMusicController>();
        if (radio == null)
            return;

        switch (action)
        {
            case ButtonAction.PreviousTrack:
                radio.PreviousTrack();
                break;
            case ButtonAction.NextTrack:
                radio.NextTrack();
                break;
            default:
                radio.TogglePlayPause();
                break;
        }
    }
}
