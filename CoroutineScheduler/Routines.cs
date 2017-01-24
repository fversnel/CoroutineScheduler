#if UNITY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RamjetAnvil.Coroutine
{
    public static class Routines {

        public delegate TimeSpan DeltaTime();
        public delegate float Animation(float lerp);

        public static readonly Animation LinearAnimation = value => value;
        public static readonly Animation EaseInOutAnimation = t => {
            t = Mathf.Clamp01(t);
            t *= 2f;
            if (t < 1f) return 0.5f * t * t * t * t;
            t -= 2f;
            return -0.5f * (t * t * t * t - 2f);
        };

        public static Animation Reverse(this Animation animation) {
            return value => animation(1f - value);
        }
        
        /// <summary>
        /// Animates an arbitrary thing over time from a start value to an end value.
        /// By default a linear from 0-1 transition is used.
        /// </summary>
        /// <param name="deltaTime">the time passed the last frame</param>
        /// <param name="duration">the total duration of the animation</param>
        /// <param name="animator">the thing to animate</param>
        /// <param name="animation">if not specified a linear animation is used</param>
        /// <returns>a schedulable routine</returns>
        public static IEnumerator<WaitCommand> Animate(DeltaTime deltaTime, TimeSpan duration, Action<float> animator, Animation animation = null) {
            animation = animation ?? LinearAnimation;

            var durationInS = (float)duration.TotalSeconds;
            var timePassed = 0f;
            while (timePassed < durationInS) {
                animator(animation(timePassed / durationInS));
                timePassed += (float)deltaTime().TotalSeconds;
                yield return WaitCommand.WaitForNextFrame;
            }
            animator(animation(1f));
        }

        public static IEnumerator<WaitCommand> Rotate(DeltaTime deltaTime, Transform transform, Quaternion target, TimeSpan duration, Animation animation = null) {
            animation = animation ?? EaseInOutAnimation;

            var startRotation = transform.rotation;
            return Animate(deltaTime, duration, lerp => transform.rotation = Quaternion.Slerp(startRotation, target, lerp), animation);
        }

        public static IEnumerator<WaitCommand> Translate(DeltaTime deltaTime, Transform transform, Vector3 newPosition, TimeSpan duration, Animation animation = null) {
            animation = animation ?? EaseInOutAnimation;

            var startPositon = transform.position;
            return Animate(deltaTime, duration, lerp => transform.position = Vector3.Lerp(startPositon, newPosition, lerp), animation);
        }

    }
}
#endif
