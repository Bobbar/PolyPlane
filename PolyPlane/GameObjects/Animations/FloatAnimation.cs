namespace PolyPlane.GameObjects.Animations
{
    public class FloatAnimation : Animation<float>
    {
        public FloatAnimation(float start, float end, float duration, Func<float, float> easeFunc, Action<float> setValFunc) : base(start, end, duration, easeFunc, setValFunc) { }

        public override void DoStep(float factor)
        {
            var newVal = Start + (End - Start) * factor;
            _setVal(newVal);

        }
    }
}
