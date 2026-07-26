using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class MainMenuCameraDirector : MonoBehaviour
{
    private struct Shot
    {
        public Vector3 position;
        public Vector3 lookAt;

        public Shot(Vector3 position, Vector3 lookAt)
        {
            this.position = position;
            this.lookAt = lookAt;
        }
    }

    private readonly List<Shot> shots = new();
    private readonly List<int> shuffleBag = new();
    private Camera showcaseCamera;
    private CanvasGroup scenicFade;
    private Coroutine rotationRoutine;
    private int lastShot = -1;

    public void Begin(Camera targetCamera, RoadSpline road, Transform van, CanvasGroup fade)
    {
        showcaseCamera = targetCamera;
        scenicFade = fade;
        BuildShots(road, van);
        if (shots.Count == 0)
            return;

        if (rotationRoutine != null)
            StopCoroutine(rotationRoutine);
        rotationRoutine = StartCoroutine(RotateShots());
    }

    public void Stop()
    {
        if (rotationRoutine != null)
            StopCoroutine(rotationRoutine);
        rotationRoutine = null;
        if (scenicFade != null)
            scenicFade.alpha = 0f;
    }

    private void BuildShots(RoadSpline road, Transform van)
    {
        shots.Clear();
        if (van != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(van.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 focus = van.position + Vector3.up * 1.15f;
            shots.Add(new Shot(
                van.position + right * 6.8f + forward * 5.2f + Vector3.up * 2.5f,
                focus + forward * 0.8f));
            shots.Add(new Shot(
                van.position - right * 5.5f - forward * 7f + Vector3.up * 3.1f,
                focus + forward * 2.2f));
        }

        if (road == null)
            return;

        AddRoadShot(road, 0.035f, -8f, 3.6f, 0.025f);
        AddRoadShot(road, 0.085f, 15f, 8.5f, 0.035f);
        AddRoadShot(road, 0.145f, -24f, 14f, 0.05f);
        AddRoadShot(road, 0.22f, 10f, 5.2f, 0.03f);
    }

    private void AddRoadShot(
        RoadSpline road,
        float t,
        float sideOffset,
        float height,
        float lookAheadT)
    {
        road.GetFrame(t, out Vector3 center, out _, out Vector3 right);
        Vector3 lookAt = road.Evaluate(Mathf.Clamp01(t + lookAheadT));
        shots.Add(new Shot(
            center + right * sideOffset + Vector3.up * height,
            lookAt + Vector3.up * 1.2f));
    }

    private IEnumerator RotateShots()
    {
        scenicFade.alpha = 1f;
        SelectNextShot();
        yield return Fade(1f, 0f, 0.8f);

        while (true)
        {
            float elapsed = 0f;
            Vector3 drift = showcaseCamera.transform.right * 0.65f;
            Vector3 start = showcaseCamera.transform.position;
            while (elapsed < 6.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 6.5f);
                showcaseCamera.transform.position = Vector3.Lerp(start, start + drift, t);
                yield return null;
            }

            yield return Fade(0f, 1f, 0.45f);
            SelectNextShot();
            yield return Fade(1f, 0f, 0.75f);
        }
    }

    private void SelectNextShot()
    {
        if (shuffleBag.Count == 0)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                if (i != lastShot || shots.Count == 1)
                    shuffleBag.Add(i);
            }
            for (int i = shuffleBag.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                (shuffleBag[i], shuffleBag[swap]) = (shuffleBag[swap], shuffleBag[i]);
            }
        }

        int index = shuffleBag[^1];
        shuffleBag.RemoveAt(shuffleBag.Count - 1);
        lastShot = index;
        Shot shot = shots[index];
        showcaseCamera.transform.SetPositionAndRotation(
            shot.position,
            Quaternion.LookRotation(shot.lookAt - shot.position, Vector3.up));
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (scenicFade == null)
            yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            scenicFade.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        scenicFade.alpha = to;
    }
}
