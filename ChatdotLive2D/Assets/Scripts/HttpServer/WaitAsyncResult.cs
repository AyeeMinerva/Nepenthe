using System;
using UnityEngine;

namespace ChatdotLive2D.Utilities
{
    // Auxiliary class for yielding on IAsyncResult in a coroutine
    public class WaitAsyncResult : CustomYieldInstruction
    {
        private IAsyncResult asyncResult;

        public WaitAsyncResult(IAsyncResult asyncResult) => this.asyncResult = asyncResult;

        // Keep waiting as long as the async operation is not completed
        public override bool keepWaiting => !asyncResult.IsCompleted;
    }
}