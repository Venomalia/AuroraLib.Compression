namespace AuroraLib.Compression.Interfaces
{
    /// <summary>
    /// Defines settings for LZ-based compression algorithms, allowing for customization of compression behavior.
    /// </summary>
    public interface ILzSettings
    {
        /// <summary>
        /// Enables the algorithm to analyze repeating patterns more effectively, resulting in more efficient compression.
        /// <br/> 
        /// Note: This may cause errors if the specific implementation does not support LookAhead optimization.
        /// </summary>
        bool LookAhead { get; set; }
    }
}
