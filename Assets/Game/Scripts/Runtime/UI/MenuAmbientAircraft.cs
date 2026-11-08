using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Purely decorative aircraft for the main menu backdrop: drifts along a gently meandering heading and wraps
    /// around the camera view. No gameplay, no collisions.
    /// </summary>
    public sealed class MenuAmbientAircraft : MonoBehaviour
    {
        private const float RotorSpinDegPerSec = 900f;

        private Camera _cam;
        private Transform _shadow;
        private Transform _rotor;
        private Vector2 _position;
        private Vector2 _shadowOffset;
        private float _headingDeg;
        private float _speed;
        private float _turnAmplitude;
        private float _turnFrequency;
        private float _phase;
        private float _time;
        private float _rotorAngle;
        private float _wrapMargin;

        /// <summary>
        /// Creates a decorative aircraft of <paramref name="type"/> (body tinted with its landing color, soft shadow,
        /// spinning rotor for helicopters) under <paramref name="parent"/>.
        /// </summary>
        public static MenuAmbientAircraft Create(AircraftType type, Camera cam, Vector2 position, float headingDeg, float speed,
            float scale, Vector2 shadowOffset, Transform parent)
        {
            if (type == null || type.bodySprite == null) return null;

            GameObject go = new GameObject("MenuAircraft_" + type.id);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer body = go.AddComponent<SpriteRenderer>();
            body.sprite = type.bodySprite;
            body.color = type.Color;
            body.sortingOrder = SortOrder.Aircraft;

            GameObject shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform, false);
            SpriteRenderer shadow = shadowGo.AddComponent<SpriteRenderer>();
            shadow.sprite = type.bodySprite;
            shadow.color = new Color(0f, 0f, 0f, 0.28f);
            shadow.sortingOrder = SortOrder.Shadow;

            Transform rotorTf = null;
            if (type.rotorSprite != null)
            {
                GameObject rotorGo = new GameObject("Rotor");
                rotorGo.transform.SetParent(go.transform, false);
                SpriteRenderer rotor = rotorGo.AddComponent<SpriteRenderer>();
                rotor.sprite = type.rotorSprite;
                rotor.color = new Color(1f, 1f, 1f, 0.9f);
                rotor.sortingOrder = SortOrder.AircraftRotor;
                rotorTf = rotorGo.transform;
            }

            MenuAmbientAircraft a = go.AddComponent<MenuAmbientAircraft>();
            a._cam = cam;
            a._shadow = shadowGo.transform;
            a._rotor = rotorTf;
            a._position = position;
            a._headingDeg = headingDeg;
            a._speed = speed;
            a._shadowOffset = shadowOffset;
            a._turnAmplitude = Random.Range(8f, 18f);
            a._turnFrequency = Random.Range(0.12f, 0.3f);
            a._phase = Random.Range(0f, Mathf.PI * 2f);
            a._rotorAngle = Random.Range(0f, 360f);
            Bounds b = type.bodySprite.bounds;
            a._wrapMargin = Mathf.Max(b.extents.x, b.extents.y) * scale + Mathf.Abs(shadowOffset.x) + Mathf.Abs(shadowOffset.y) + 0.2f;
            a.ApplyTransform();
            return a;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            _headingDeg += Mathf.Sin(_time * _turnFrequency * Mathf.PI * 2f + _phase) * _turnAmplitude * dt;

            float r = _headingDeg * Mathf.Deg2Rad;
            _position += new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * (_speed * dt);

            if (_cam != null)
            {
                Rect view = PlayBounds.ViewRect(_cam);
                float m = _wrapMargin;
                if (_position.x > view.xMax + m) _position.x = view.xMin - m;
                else if (_position.x < view.xMin - m) _position.x = view.xMax + m;
                if (_position.y > view.yMax + m) _position.y = view.yMin - m;
                else if (_position.y < view.yMin - m) _position.y = view.yMax + m;
            }

            if (_rotor != null)
            {
                _rotorAngle = Mathf.Repeat(_rotorAngle - RotorSpinDegPerSec * dt, 360f);
            }

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            transform.SetPositionAndRotation(new Vector3(_position.x, _position.y, 0f), Quaternion.Euler(0f, 0f, _headingDeg));
            // The shadow rotates with the body but its offset stays in world space (constant light direction).
            _shadow.position = new Vector3(_position.x + _shadowOffset.x, _position.y + _shadowOffset.y, 0f);
            if (_rotor != null) _rotor.localRotation = Quaternion.Euler(0f, 0f, _rotorAngle);
        }
    }
}
