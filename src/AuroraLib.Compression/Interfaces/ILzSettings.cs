namespace AuroraLib.Compression.Interfaces
{
    public interface ILzSettings
    {
        /// <summary>
        /// Allows the algorithm to analyze future data for more efficient compression.
        /// </summary>
        bool LookAhead { get; set; }
    }
}
