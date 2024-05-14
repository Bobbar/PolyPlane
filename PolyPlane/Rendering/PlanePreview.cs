using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public class PlanePreview : IDisposable
    {
        public D2DColor PlaneColor = D2DColor.Yellow;

        private D2DDevice _device;
        private D2DGraphics _gfx;
        private RenderContext _ctx;

        private Control _targetControl;
        private Thread _renderThread;
        private bool disposedValue;
        private D2DColor _clearColor = D2DColor.SkyBlue;
        private FighterPlane _plane;
        private const float VIEW_SCALE = 1f;
        private float _angle = 0f;

        public PlanePreview(Control target, D2DColor planeColor)
        {
            _targetControl = target;

            InitGfx();

            PlaneColor = planeColor;
            _plane = new FighterPlane(new D2DPoint(0, -20f), PlaneColor);
            _plane.IsNetObject = true;

            _renderThread = new Thread(RenderLoop);
            _renderThread.Start();

        }

        private void RenderLoop()
        {
            var center = new D2DPoint(_targetControl.Width / 2f, _targetControl.Height / 2f);
            var vpSize = new D2DSize(_targetControl.Width / VIEW_SCALE, _targetControl.Height / VIEW_SCALE);
            var viewPortRect = new D2DRect(center, vpSize).Inflate(400f, 400f);
            _ctx.Viewport = viewPortRect;

            while (!disposedValue)
            {
                _gfx.BeginRender(_clearColor);
                _gfx.PushTransform();
                _gfx.TranslateTransform(0f, 90f);

                _plane.PlaneColor = PlaneColor;
                _plane.Update(World.DT, World.RenderScale);
                _plane.Position = center;
                _angle = Utilities.ClampAngle(_angle + 1f);
                _plane.Rotation = _angle;
                _plane.Render(_ctx);

                _gfx.PopTransform();
                _gfx.EndRender();
            }
        }

        private void InitGfx()
        {
            _device?.Dispose();
            _device = D2DDevice.FromHwnd(_targetControl.Handle);
            _gfx = new D2DGraphics(_device);
            _gfx.Antialias = true;
            _device.Resize();
            _ctx = new RenderContext(_gfx, _device);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;

                _renderThread?.Join(150);
                _device?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
