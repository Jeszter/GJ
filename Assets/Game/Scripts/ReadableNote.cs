using UnityEngine;

namespace GJ.Game
{
    // A scrap of paper the player can read with E. Carries the backstory and, quietly, the puzzle's hint.
    public class ReadableNote : MonoBehaviour
    {
        [TextArea(4, 12)] public string text;
    }
}
