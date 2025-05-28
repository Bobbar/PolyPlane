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
        private readonly D2DPoint _planePosition = new D2DPoint(0f, -100f);

        public PlanePreview(Control target, D2DColor planeColor)
        {
            _targetControl = target;

            InitGfx();

            PlaneColor = planeColor;
            _plane = new FighterPlane(_planePosition, PlaneColor);
            World.ObjectManager.Clear();

            _renderThread = new Thread(RenderLoop);
            _renderThread.IsBackground = true;
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
                _gfx.TranslateTransform(100f, 200f);

                _plane.PlaneColor = PlaneColor;
                _plane.Position = _planePosition;
                _angle = Utilities.ClampAngle(_angle + 1f);
                _plane.Rotation = _angle;
                _plane.Velocity = D2DPoint.Zero;
                _plane.Update(World.TargetDT);
                _plane.Render(_ctx);

                // Draw a crosshair to preview HUD color.
                _gfx.DrawCrosshair(_planePosition, 5f, World.HudColor, 8f, 30f);

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
