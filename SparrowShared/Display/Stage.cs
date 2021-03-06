

using Sparrow.Geom;
using System;
using Sparrow.Filters;

namespace Sparrow.Display
{
    /// <summary>
    /// A Stage is the root of the display tree. It represents the rendering area of the application.
    /// <para>
    /// Sparrow will create the stage for you. You can access 'Stage' from anywhere with SparrowSharp.Stage.
    /// </para>
    /// <para>
    /// The Stage's 'StageWidth' and 'StageHeight' values define the coordinate system of your app. The color
    /// of the stage defines the background color of your app.
    /// </para>
    /// <para>
    /// The Stage's Width and Height properties return the enclosing bounds of your app. This can be bigger than
    /// StageWidth/StageHeight if you place objects outside the Stage coordinates.
    /// </para>
    /// </summary>
    public class Stage : DisplayObjectContainer
    {
        public delegate void ResizeHandler(DisplayObject target);

        /// <summary>
        /// Dispatched when the drawable area changes, e.g. on window resize on PCs or device rotation on mobile.
        /// </summary>
        public event ResizeHandler OnResize;

        /// <summary>
        /// The background color of the stage. Default: black.
        /// </summary>
        public uint Color { get; set; }

        /// <summary>
        /// Specifies an angle (radian, between zero and PI) for the field of view. This value
        /// determines how strong the perspective transformation and distortion apply to a Sprite3D
        /// object.
        ///
        /// <para>A value close to zero will look similar to an orthographic projection; a value
        /// close to PI results in a fisheye lens effect. If the field of view is set to 0 or PI,
        /// nothing is seen on the screen.</para>
        ///
        /// Default 1.0
        /// </summary>
        public float FieldOfView { get; set; }
        private readonly Point _projectionOffset;
        private float _width;
        private float _height;
        /// <summary>
        /// Initializes a stage with a certain size in points. Sparrow calls this automatically on startup.
        /// </summary>
        internal Stage(float width, float height)
        {
            _width = width;
            _height = height;
            Color = 0xFFFFFF;
            FieldOfView = 1.0f;
            _projectionOffset = Point.Create();
        }

        public override DisplayObject HitTest(Point localPoint)
        {
            if (!Visible || !Touchable)
            {
                return null;
            }

            // locations outside of the stage area shouldn't be accepted
            if (localPoint.X < 0 || localPoint.X > _width ||
                localPoint.Y < 0 || localPoint.Y > _height)
            {
                return null;
            }
            // if nothing else is hit, the stage returns itself as target
            DisplayObject target = base.HitTest(localPoint);
            if (target == null)
            {
                target = this;
            }
            return target;
        }

        /// <summary>
        /// Returns the stage bounds (i.e. not the bounds of its contents, but the rectangle
        /// spawned up by 'StageWidth' and 'StageHeight') in another coordinate system.
        /// </summary>
        public Rectangle GetStageBounds(DisplayObject targetSpace)
        {
            Rectangle outR = Rectangle.Create(0, 0, _width, _height);

            Matrix2D sMatrix = GetTransformationMatrix(targetSpace);

            return outR.GetBounds(sMatrix);
        }

        // camera positioning

        /// <summary>
        /// Returns the position of the camera within the local coordinate system of a certain
        /// display object. If you do not pass a space, the method returns the global position.
        /// To change the position of the camera, you can modify the properties 'fieldOfView',
        /// 'focalDistance' and 'projectionOffset'.
        /// </summary>
        public float[] GetCameraPosition(DisplayObject space = null)
        {
            Matrix3D m = GetTransformationMatrix3D(space);

            return m.TransformCoords3D(
                _width / 2 + _projectionOffset.X, _height / 2 + _projectionOffset.Y,
                -FocalLength);
        }


        internal void AdvanceTime(float passedTime)
        {
            BroadcastEnterFrameEvent(passedTime);
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float Width
        {
            set => throw new InvalidOperationException("cannot set width of stage. Use StageWidth instead.");
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float Height
        {
            set => throw new InvalidOperationException("cannot set height of stage. Use StageHeight instead.");
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float X
        {
            set => throw new InvalidOperationException("cannot set x-coordinate of stage");
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float Y
        {
            set => throw new InvalidOperationException("cannot set y-coordinate of stage");
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float ScaleX
        {
            set => throw new InvalidOperationException("cannot scale stage");
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float ScaleY
        {
            set => throw new InvalidOperationException("cannot scale stage");
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float Rotation
        {
            set => throw new InvalidOperationException("cannot set rotation of stage");
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float SkewX
        {
            set => throw new InvalidOperationException("cannot skew stage");
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float SkewY
        {
            set => throw new InvalidOperationException("cannot skew stage");
        }

        public override FragmentFilter Filter
        {
            get { throw new InvalidOperationException("Cannot add filter to stage. Add it to 'root' instead!"); }
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float PivotX
        {
            set => throw new InvalidOperationException("cannot set PivotX of stage");
        }

        /// <summary>
        /// Cannot be set on the Stage, trying to set it will throw an exception
        /// </summary>
        public override float PivotY
        {
            set => throw new InvalidOperationException("cannot set PivotY of stage");
        }

        /// <summary>
        /// The height of the stage's coordinate system.
        /// Changing Stage size does not affect the size of the rendered area. By default its the same as SparrowSharp.ViewPort.Width,
        /// in this case 1 unit in the Stage equals 1 pixel.
        /// </summary>
        public float StageWidth {
            get => _width;
            set => _width = value;
        }

        /// <summary>
        /// The width of the stage's coordinate system.
        /// Changing Stage size does not affect the size of the rendered area. By default its the same as SparrowSharp.ViewPort.Height,
        /// in this case 1 unit in the Stage equals 1 pixel.
        /// </summary>
        public float StageHeight {
            get => _height;
            set => _height = value;
        }

        /// <summary>
        /// The distance between the stage and the camera. Changing this value will update the
        /// field of view accordingly.
        /// </summary>
        public float FocalLength
        {
            get => StageWidth / (2f * (float)Math.Tan(FieldOfView / 2f));
            set => FieldOfView = 2 * (float)Math.Atan(StageWidth / (2f * value));
        }

        /// <summary>
        /// A vector that moves the camera away from its default position in the center of the
        /// stage. Use this property to change the center of projection, i.e. the vanishing
        /// point for 3D display objects. <para>CAUTION: not a copy, but the actual object!</para>
        /// </summary>
        public Point ProjectionOffset {
            get => _projectionOffset;
            set => _projectionOffset.SetTo(value.X, value.Y);
        }

        /// <summary>
        /// The global position of the camera. This property can only be used to find out the
        /// current position, but not to modify it. For that, use the 'projectionOffset',
        /// 'fieldOfView' and 'focalLength' properties. If you need the camera position in
        /// a certain coordinate space, use 'getCameraPosition' instead.
        ///
        /// <para>CAUTION: not a copy, but the actual object!</para>
        /// </summary>
        public float[] CameraPosition => GetCameraPosition();
    }
}