//Date of last modification: 26/10/2024 10:48:56
using UnityEngine;

namespace PitGL
{
	public partial class VectorDistribution
	{
		public static VectorDistributionParams Distribution_SSAO = new VectorDistributionParams()
		{
			//XZ Angle
			revolutions = 20f,

			//Y Angle
			minAngle = 5f,
			maxAngle = 37f,
			verticalPerturbation = 0.5f,

			//Radius
			minDepth = 0.125f,
			maxDepth = 1f,
			depthExponent = 0.9375f,
		};

		public static void CreateVectors_SSAO(VectorDistributionParams distribution, SampleCount sampleCount, int patternRadius, out Vector3[] vectors)
		{
			switch (sampleCount)
			{
				default:
				case SampleCount.x1:
					vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, 0);
					break;
				case SampleCount.x2:
					vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, 356205);
					break;
				case SampleCount.x3:
					vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, 228316);
					break;
				case SampleCount.x4:
					vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, 139749);
					break;
				case SampleCount.x8:
					vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, 383690);
					break;
				case SampleCount.x12:
					vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, 12264);
					break;
				case SampleCount.x16:
					vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, 135765);
					break;
				case SampleCount.x32:
					vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, 198233);
					break;
				case SampleCount.x64:
					vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, 103303);
					break;
			}
		}
	}
}
