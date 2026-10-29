using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Cheon.Prototype.MountainPass;

public static class InspectTitleTouch
{
    public static async Task<object> Main()
    {
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var hud = UnityEngine.Object.FindFirstObjectByType<PassHud>();
        var button = hud.transform.Find("MountainPassHUD/Introduction/TitleColumn/BeginJourney").GetComponent<UnityEngine.UI.Button>();
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var rays = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, rays);
        int clicked = 0;
        button.onClick.AddListener(() => clicked++);
        var touch = InputSystem.AddDevice<Touchscreen>();
        await Task.Delay(200);
        InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point, pressure = 1f });
        await Task.Delay(200);
        Vector2 actual = touch.primaryTouch.position.ReadValue();
        InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
        await Task.Delay(300);
        InputSystem.RemoveDevice(touch);
        return new { screen = new { Screen.width, Screen.height }, requested = new { point.x, point.y }, actual = new { actual.x, actual.y }, hit = rays.Select(ray => ray.gameObject.name).ToArray(), clicked, button.interactable, state = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>().State.ToString() };
    }
}
