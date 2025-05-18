using System.Collections.Generic;
using System.Linq; // For ToLowerInvariant()

namespace ChatdotLive2D.Data
{
    public class EmotionMapping
    {
        private readonly Dictionary<string, int> emotionToInput;

        public EmotionMapping()
        {
            // Initialize the mapping data
            emotionToInput = new Dictionary<string, int>()
            {
                // ... (Your mapping remains the same) ...
                { "happy", 1 }, { "sad", 2 }, { "angry", 3 }, { "sleepy", 4 },
                { "admiration", 5 }, { "amusement", 6 }, { "anger", 3 }, { "annoyance", 8 },
                { "approval", 9 }, { "caring", 1 }, { "confusion", 17 }, { "curiosity", 17 },
                { "desire", 13 }, { "disappointment", 14 }, { "disapproval", 2 }, { "disgust", 16 },
                { "embarrassment", 17 }, { "excitement", 18 }, { "fear", 2 }, { "gratitude", 1 },
                { "grief", 21 }, { "joy", 22 }, { "love", 1 }, { "nervousness", 17},
                { "optimism", 1 }, { "pride", 1 }, { "realization", 27 }, { "relief", 28 },
                { "remorse", 2 }, { "sadness", 2 }, { "surprise", 31 }, { "neutral", 1 }
            };
        }

        /// <summary>
        /// Tries to get the animation input integer for a given emotion string.
        /// Case-insensitive lookup.
        /// </summary>
        /// <param name="emotion">The emotion string.</param>
        /// <param name="input">The corresponding animation input integer.</param>
        /// <returns>True if a mapping was found, false otherwise.</returns>
        public bool TryGetAnimationInput(string emotion, out int input)
        {
            if (string.IsNullOrEmpty(emotion))
            {
                input = -1;
                return false;
            }
            return emotionToInput.TryGetValue(emotion.ToLowerInvariant(), out input);
        }
    }
}