﻿using System.Collections.Generic;
using Sparrow.Display;

namespace Sparrow.Touches
{
    /*
     When one or more fingers touch the screen, move around or are raised, an SPTouchEvent is triggered.
     
     The event contains a list of all touches that are currently present. Each individual touch is 
     stored in an object of type "Touch". Since you are normally only interested in the touches 
     that occurred on top of certain objects, you can query the event for touches with a 
     specific target through the 'TouchesWithTarget' method. In this context, the target of a 
     touch is not only the object that was touched (e.g. an Image), but also each of its parents - 
     e.g. the container that holds that image.
     
     Here is an example of how to react on touch events at 'self', which could be a subclass of SPSprite:

    // e.g. in 'init'
    [self addEventListener:@selector(onTouch:) atObject:self forType:SPEventTypeTouch];
    
    // the corresponding listener:
    - (void)onTouch:(SPTouchEvent *)event
    {
        // query all touches that are currently moving on top of 'self'
        NSArray *touches = [[event touchesWithTarget:self andPhase:SPTouchPhaseMoved] allObjects];
    
        if (touches.count == 1)
        {
            // one finger touching
            SPTouch *touch = [touches objectAtIndex:0];
            SPPoint *currentPos = [touch locationInSpace:self.parent];
            SPPoint *previousPos = [touch previousLocationInSpace:self.parent];
            // ...
        }
        else if (touches.count >= 2)
        {
            // two fingers touching
            // ...
        }
    }
    */ 
    public class TouchEvent
    {
        public TouchEvent (List<Touch> touches = null, bool bubbles = true)
        {
            if (touches == null) {
                touches = new List<Touch> ();
            }
            _touches = touches;
        }


        /// <summary>
        /// Gets a set of Touch objects that originated over a certain target.
        /// </summary>
        public List<Touch> GetTouches(DisplayObject target)
        {
            List<Touch> touchesFound = new List<Touch>();
            foreach (Touch touch in _touches) {
                if (target == touch.Target || 
                   (target is DisplayObjectContainer && 
                    ((DisplayObjectContainer)target).Contains(touch.Target)))
                {
                    touchesFound.Add(touch);
                }
            }
            return touchesFound;
        }

        /// <summary>
        /// Gets a set of Touch objects that originated over a certain target and are in a certain phase.
        /// </summary>
        public List<Touch> GetTouches(DisplayObject target, TouchPhase phase)
        {
            List<Touch> touchesFound = new List<Touch>();
            foreach (Touch touch in _touches) {
                if (touch.Phase == phase &&
                   (target == touch.Target || 
                   (target is DisplayObjectContainer && ((DisplayObjectContainer)target).Contains(touch.Target))))
                {
                    touchesFound.Add(touch);
                }
            }
            return touchesFound;
        }
        
        /// <summary>
        /// Returns a touch that originated over a certain target.
        /// </summary>
        /// <param name="target">The object that was touched; may also be a parent of the actual touch-target.</param>
        public Touch GetTouch(DisplayObject target)
        {
            var sTouches = GetTouches(target);
            if (sTouches.Count > 0) 
            {
                return sTouches[0];
            }
            return null;
        }
        
        /// <summary>
        /// Indicates if a target is currently being touched or hovered over.
        /// </summary>
        public bool InteractsWith(DisplayObject target)
        {
            var result = false;
            var sTouches = GetTouches(target);
            
            for (var i = sTouches.Count - 1; i >= 0; --i)
            {
                if (sTouches[i].Phase != TouchPhase.Ended)
                {
                    result = true;
                    break;
                }
            }
            sTouches.Clear();
            return result;
        }

        private List<Touch> _touches;
        /// All touches that are currently available.
        public List<Touch> Touches {
            get {
                return _touches;
            }
            internal set {
                _touches = value;
            }
        }

        private double _timestamp;
        // The time the event occurred (in seconds since application launch).
        public double Timestamp {
            get {
                return _touches[0].TimeStamp;
            }
            internal set {
                _timestamp = value;
            }
        }

        public override string ToString()
        {
            string str = "touch timestamp " + _timestamp;
            foreach (var touch in Touches)
            {
                str += $"\n id: {touch.TouchID} target: {touch.Target} phase: {touch.Phase} target {touch.Target} " +
                       $"globalX: {touch.GlobalX} globalY: {touch.GlobalY}";
            }
            return str;
        }

    }
}

