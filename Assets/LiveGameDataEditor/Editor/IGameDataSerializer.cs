namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Contract for serializing and deserializing any <see cref="IGameDataContainer" />.
    ///     Implement this interface to support alternative formats (CSV, binary, etc.).
    ///     The default implementation is <see cref="GameDataJsonSerializer" />.
    /// </summary>
    public interface IGameDataSerializer
    {
        /// <summary>Serializes all entries in <paramref name="container" /> to a string.</summary>
        /// <param name="container">The data source to serialize.</param>
        /// <param name="indented">When <c>true</c>, output is formatted for human readability.</param>
        string Serialize(IGameDataContainer container, bool indented = true);

        /// <summary>
        ///     Deserializes <paramref name="json" /> and replaces the entries in
        ///     <paramref name="container" />. The caller is responsible for Undo recording
        ///     before invoking this method.
        /// </summary>
        void Deserialize(string json, IGameDataContainer container);
    }
}