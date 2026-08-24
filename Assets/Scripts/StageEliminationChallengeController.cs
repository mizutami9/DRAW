using UnityEngine;

namespace DrawBody.Prototype
{
    public abstract class StageEliminationChallengeController : MonoBehaviour
    {
        public virtual bool UsesGlobalFallBoundary => true;

        public abstract void RequestElimination(PlayerController2D target);
    }
}
