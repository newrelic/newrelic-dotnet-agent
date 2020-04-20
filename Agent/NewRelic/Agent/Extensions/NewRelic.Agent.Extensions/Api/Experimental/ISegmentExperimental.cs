namespace NewRelic.Agent.Api.Experimental
{
	/// <summary>
	/// This interface contains methods we may eventually move to <see cref="ISegment"/> once they have been sufficiently vetted.
	/// Methods on this interface are subject to refactoring or removal in future versions of the API.
	/// </summary>
	public interface ISegmentExperimental
	{
		/// <summary>
		/// Gets the ISegmentData currently associated to the segment. This is useful when the logic for managing
		/// the segment data is split across multiple instrumentation classes.
		/// </summary>
		ISegmentData SegmentData { get; }

		/// <summary>
		/// Adds the provided segmentData to the segment. This data replaces any previously set segmentData
		/// on the segment. This data should be added before the segment ends.
		/// </summary>
		/// <param name="segmentData">The data to add to the segment.</param>
		/// <returns>The segment that the segmentData was added to.</returns>
		ISegmentExperimental SetSegmentData(ISegmentData segmentData);

		/// <summary>
		/// Makes the segment a leaf segment. Leaf segments will prevent other
		/// instrumented methods from running while the leaf segment is currently on the call stack.
		/// </summary>
		/// <returns>The segment that the segmentData was added to.</returns>
		ISegmentExperimental MakeLeaf();

	}
}
