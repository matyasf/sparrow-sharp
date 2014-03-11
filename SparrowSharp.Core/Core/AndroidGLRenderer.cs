﻿using System;

using Android.Views;
using Android.Content;
using Android.Util;
using Android.Opengl;
using Android.OS;

using Java.Lang;
using Sparrow;

namespace GLNativeES20
{
	public class AndroidGLRenderer : Java.Lang.Object, GLSurfaceView.IRenderer
	{

		#region IRenderer implementation
		public void OnSurfaceCreated (Javax.Microedition.Khronos.Opengles.IGL10 gl, Javax.Microedition.Khronos.Egl.EGLConfig config)
		{
			GLES20.GlDisable(GLES20.GlCullFaceMode);
			GLES20.GlDisable(GLES20.GlDepthTest);
			GLES20.GlDisable(GLES20.GlBlend);
		}

		public void OnDrawFrame (Javax.Microedition.Khronos.Opengles.IGL10 gl)
		{
			SP.Step();
			// calls SwapBuffers automatically
		}

		public void OnSurfaceChanged (Javax.Microedition.Khronos.Opengles.IGL10 gl, int width, int height)
		{
			// Adjust the viewport based on geometry changes, such as screen rotation
			GLES20.GlViewport (0, 0, width, height);
			SP.InitApp (width, height);
		}
		#endregion

	}

}

