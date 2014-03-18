using System;
using Sparrow.Display;
using Sparrow.Textures;
using SparrowSharp.Samples.Desktop;
using Sparrow.ResourceLoading;

namespace Sparrow.Samples.Desktop
{
	public class Benchmark : Sprite
	{
	    readonly Texture _texture;
	    readonly Sprite _container;
		int _frameCount = 0;
		float _elapsed = 0;
		bool _started = false;
		int _failCount = 0;
		int _waitFrames = 0;

		public Benchmark ()
		{
			TextureLoader loader = new TextureLoader ();
			loader.LoadLocalResource ("../../benchmark_object.png");
			_texture = loader.GetResource ();
			// the container will hold all test objects
			_container = new Sprite ();
			AddChild (_container);

			EnterFrame += EnterFrameHandler;
			AddedToStage += AddedToStageHandler;
		}

		void AddTestObjects (int numObjects)
		{
			int border = 15;

			Random r = new Random ();
			for (int i = 0; i < numObjects; ++i) {   
				Sparrow.Display.Image egg = new Sparrow.Display.Image (_texture);
				egg.X = r.Next (border, (int)Stage.Width - border);
				egg.Y = r.Next (border, (int)Stage.Height - border);
				egg.Rotation = (float)(r.Next (0, 100) / 100 * Math.PI);
				_container.AddChild (egg);
			}
		}

		void BenchmarkComplete ()
		{
			_started = false;

			Console.WriteLine ("benchmark complete!");
			Console.WriteLine ("number of objects: " + _container.NumChildren);
		}

		void AddedToStageHandler (DisplayObject target, DisplayObject currentTarget)
		{
			_started = true;
			_waitFrames = 30;
			AddTestObjects (15000);
		}

		void EnterFrameHandler (DisplayObject target, DisplayObject currentTarget, float passedTime)
		{
			if (!_started)
				return;

			_elapsed += passedTime / 1000;
			++_frameCount;

			if (_frameCount % _waitFrames == 0) {
				float targetFPS = 60;
				float realFPS = _waitFrames / _elapsed;
                Console.WriteLine("FPS:" + realFPS);
				/*if (realFPS >= targetFPS) {
					int numObjects = _failCount != 0 ? 5 : 25;
					AddTestObjects (numObjects);
					_failCount = 0;
				} else {
					++_failCount;

					if (_failCount > 15)
						_waitFrames = 5; // slow down creation process to be more exact
					if (_failCount > 20)
						_waitFrames = 10;
					if (_failCount == 25)
						BenchmarkComplete (); // target fps not reached for a while
				}*/

				_elapsed = _frameCount = 0;
			}

			for (int i = 0; i < _container.NumChildren; i++) {
				DisplayObject child = _container.GetChild (i);
				child.Rotation += 0.05f;    
			}
		}
	}
}