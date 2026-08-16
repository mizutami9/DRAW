using UnityEngine;

namespace DrawBody.Prototype
{
    public abstract class StageEliminationChallengeController : MonoBehaviour
    {
        public abstract void RequestElimination(PlayerController2D target);
    }
}
