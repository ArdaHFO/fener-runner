using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FenerRunner
{
    public enum RunnerCommand { Left, Right, Jump, Slide, DebugTurn, DebugFlip }

    public sealed class RunnerInput : MonoBehaviour
    {
        [SerializeField] private RunnerSettings settings;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private Vector2 begin;
        private int finger = -1;
        private bool tracking, blocked, wasTouch;
        private PointerEventData pointer;
        private EventSystem pointerSystem;
        public event Action<RunnerCommand> Command;
        public void Configure(RunnerSettings value) { settings = value; }
        public void ResetGesture() { tracking = false; blocked = false; finger = -1; }

        public void Poll()
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Emit(RunnerCommand.Left);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Emit(RunnerCommand.Right);
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) Emit(RunnerCommand.Jump);
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Emit(RunnerCommand.Slide);
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.T)) Emit(RunnerCommand.DebugTurn);
            if (Input.GetKeyDown(KeyCode.F)) Emit(RunnerCommand.DebugFlip);
#endif
            if (Input.touchCount > 0)
            {
                wasTouch = true;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (!tracking && touch.phase == TouchPhase.Began) Begin(touch.position, touch.fingerId);
                    if (touch.fingerId != finger) continue;
                    if (touch.phase == TouchPhase.Ended) End(touch.position);
                    else if (touch.phase == TouchPhase.Canceled) ResetGesture();
                    else if (touch.phase == TouchPhase.Moved) Move(touch.position);
                }
                return;
            }
            // Ignore Unity's emulated mouse event on the touch-release frame.
            if (wasTouch) { wasTouch = false; ResetGesture(); return; }
            if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition, -1);
            if (Input.GetMouseButton(0) && tracking) Move(Input.mousePosition);
            if (Input.GetMouseButtonUp(0) && tracking) End(Input.mousePosition);
        }

        private void Begin(Vector2 position, int id)
        { begin = position; finger = id; tracking = true; blocked = OverUi(position); }
        private void Move(Vector2 position)
        {
            if(!tracking || blocked || OverUi(position))return;
            Vector2 delta = position - begin;
            if(delta.magnitude < Mathf.Min(Screen.width, Screen.height) * settings.swipeScreenFraction)return;
            ResetGesture();
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) Emit(delta.x < 0f ? RunnerCommand.Left : RunnerCommand.Right);
            else Emit(delta.y > 0f ? RunnerCommand.Jump : RunnerCommand.Slide);
        }
        private void End(Vector2 position){Move(position);ResetGesture();}
#if UNITY_EDITOR
        public void TestBegin(Vector2 position){Begin(position,7);}
        public void TestMove(Vector2 position){Move(position);}
        public void TestEnd(Vector2 position){End(position);}
#endif
        private bool OverUi(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            if (pointer == null || pointerSystem != EventSystem.current)
            {
                pointerSystem = EventSystem.current;
                pointer = new PointerEventData(pointerSystem);
            }
            pointer.position = position;
            hits.Clear();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Count > 0;
        }
        private void Emit(RunnerCommand value) { Command?.Invoke(value); }
    }
}
