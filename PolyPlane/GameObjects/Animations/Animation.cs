using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Animations
{
    public abstract class Animation<T> : GameObject
    {
        public T Start;
        public T End;

        public float Duration;
        public float AnimPostition;
        public Func<float, float> EaseFunc;

        public bool IsPlaying = false;
        public bool Loop = false;
        public bool ReverseOnLoop = false;
        public float Speed = 1.0f;
        protected Action<T> _setVal;

        protected bool _isReversed = false;
        protected float _elapsed = 0f;

        protected Animation() { }

        protected Animation(T start, T end, float duration,  Func<float, float> easeFunc, Action<T> setValFunc) 
        {
            Start = start;
            End = end;
            Duration = duration;
            EaseFunc = easeFunc;
            _setVal = setValFunc;
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            if (!IsPlaying)
                return;

            _elapsed += dt;
            AnimPostition = _elapsed / Duration;

            if (_elapsed < Duration)
            {
                var factor = EaseFunc(AnimPostition);
                DoStep(factor);
            }
            else
            {
                if (Loop)
                {
                    _elapsed = 0f;
                    AnimPostition = 0f;

                    if (ReverseOnLoop)
                        _isReversed = !_isReversed;
                }
                else
                {
                    Done();
                    IsPlaying = false;
                }
            }


        }

        public void Reset()
        {
            IsPlaying = true;
            _elapsed = 0f;
            AnimPostition = 0f;
        }

        public virtual void DoStep(float factor)
        {

        }

        public virtual void Done()
        {

        }

    }
}
