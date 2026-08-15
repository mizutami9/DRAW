namespace DrawBody.Prototype
{
    /// <summary>
    /// A stage device that remains visible when used as a link target and performs
    /// one discrete action for each authoritative link activation.
    /// </summary>
    public interface IStageLinkActivatable
    {
        void PrepareForLink();
        void ActivateFromLink();
    }
}
