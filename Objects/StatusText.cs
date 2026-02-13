using TMPro;
using UnityEngine;

namespace NeonCapture.Objects
{
    internal class StatusText : MonoBehaviour
    {
        internal static StatusText i;

        internal static readonly Color errorColor = new Color32(209, 61, 62, 255);
        internal static readonly Color warnColor = new Color32(235, 193, 6, 255);

        public class Transition(Func<float, float, float, float> ease, Action finish)
        {
            readonly Func<float, float, float, float> easeFunc = ease;
            public Action finishFunc = finish;

            public bool skip = false;
            public float start;
            public float goal;
            public float result;
            public bool running;
            public float speed = 4;
            public float time;


            public void Start(float? s, float g)
            {
                running = true;
                time = 0;
                start = s ?? result;
                result = start;
                goal = g;
            }

            public void Process()
            {
                if (time == 1f)
                {
                    time = 0;
                    result = goal;
                    finishFunc?.Invoke();
                    running = false;
                }
                if (!running)
                    return;
                time = Math.Min(1f, time + (Time.unscaledDeltaTime * speed));
                result = easeFunc(start, goal, time);
            }
        }

        readonly Transition _moveY = new(AxKEasing.EaseInOutCubic, null);
        readonly Transition _opacityT = new(AxKEasing.Linear, null);

        TextMeshProUGUI text;
        Color color = Color.white;
        public float timer = -1;
        public float lastTimer;

        void Awake()
        {
            i = this;
            // create a canvas for the text to go into
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            _moveY.finishFunc = () => timer = timer == 0 ? -1 : lastTimer;
        }

        public void Start()
        {
            text = new GameObject("Status", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.parent = transform;
            (text.transform as RectTransform).pivot = new(0, 0);
            text.margin = new Vector4(100, 0, -2000, 0);
            text.fontSize = 24;
            text.font = Resources.Load<TMP_FontAsset>("fonts/source code pro/SourceCodePro-Black SDF");
            text.outlineWidth = 0.15f;
            text.alignment = TextAlignmentOptions.TopLeft;

            _moveY.result = 20;
            _opacityT.result = 0;
            Update();
        }

        public void Update()
        {
            float size = 1f / 1080 * Screen.height;
            text.transform.position = new(10f / 1920 * Screen.width, -_moveY.result / 1080 * Screen.height);
            text.transform.localScale = new(size, size, 1);
            text.faceColor = Color.black;
            text.outlineColor = color;
            text.alpha = _opacityT.result;
            _moveY.Process();
            _opacityT.Process();
            if (!_moveY.running && timer > 0)
            {
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0)
                {
                    timer = 0;
                    _moveY.Start(10, 20);
                    _opacityT.Start(1, 0);
                }
            }
        }

        public void SetStatus(string status, int timer = 3) => SetStatus(status, Color.white, timer);
        public void SetStatus(string status, Color color, int timer = 3)
        {
            if (status != text.text)
                NeonCapture.Log.DebugMsg($"Status: {status}");
            text.text = status;
            this.color = color;
            lastTimer = this.timer = timer;
            if (_moveY.goal != 10)
                _moveY.Start(null, 10);
            if (_opacityT.goal != 1)
                _opacityT.Start(null, 1);
        }

        public void ClearStatus()
        {
            Hooks.showWaiting = false;
            _moveY.goal = _moveY.result = 20;
            _moveY.time = 0;
            _moveY.running = false;
            _opacityT.goal = _opacityT.result = 0;
            _opacityT.time = 0;
            _opacityT.running = false;
            timer = 0;
        }
    }
}
