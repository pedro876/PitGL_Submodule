using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System;

namespace PitGL
{
    public partial class VectorDistribution
    {
        //public const int MAX_SAMPLES_PER_PIXEL = 64;
        public const int MAX_SAMPLE_COUNT_VARIANTS = 9; //Must match the amount of entries in the SampleCount enum
        public const int MAX_SAMPLE_COUNT = 64; //Must match the amount of entries in the SampleCount enum & MAX_DISPLACEMENT_VECTORS_PER_PIXEL in VectorDistribution.hlsl

        public enum SampleCount
        {
            x1 = 1,
            x2 = 2,
            x3 = 3,
            x4 = 4,
            //x6 = 6,
            x8 = 8,
            x12 = 12,
            x16 = 16,
            //x24 = 24,
            x32 = 32,
            //x48 = 48,
            x64 = 64
        }

        public static SampleCount[] GetSampleCountEntries()
        {
            var sampleCountValues = Enum.GetValues(typeof(VectorDistribution.SampleCount));
            VectorDistribution.SampleCount[] sampleCounts = new VectorDistribution.SampleCount[sampleCountValues.Length];
            for (int i = 0; i < sampleCounts.Length; i++) sampleCounts[i] = (VectorDistribution.SampleCount)sampleCountValues.GetValue(i);
            return sampleCounts;
        }

#if UNITY_EDITOR
        private static StringBuilder sb;
        private static int indent;

        public static string GenerateVectorsCodeWithSeeds(string name, VectorDistributionParams distribution, List<int> bestSeeds)
        {
            indent = 0;
            sb = new StringBuilder();

            GenerateSwitchCases(name, ()=>
            {
                Line($"public static {nameof(VectorDistributionParams)} Distribution_{name} = new {nameof(VectorDistributionParams)}()");
                BracketsIn();
                {
                    Line("//XZ Angle");
                    Line($"revolutions = {FloatToStr(distribution.revolutions)}f,");
                    Space();
                    Line("//Y Angle");
                    Line($"minAngle = {FloatToStr(distribution.minAngle)}f,");
                    Line($"maxAngle = {FloatToStr(distribution.maxAngle)}f,");
                    Line($"verticalPerturbation = {FloatToStr(distribution.verticalPerturbation)}f,");
                    Space();
                    Line("//Radius");
                    Line($"minDepth = {FloatToStr(distribution.minDepth)}f,");
                    Line($"maxDepth = {FloatToStr(distribution.maxDepth)}f,");
                    Line($"depthExponent = {FloatToStr(distribution.depthExponent)}f,");
                }
                BracketsOut(true);
                Space();
            },
            (arrIndex) =>
            {
                Line($"vectors = {nameof(VectorDistributionGenerator)}.GenerateVectors(distribution, sampleCount, {bestSeeds[arrIndex]});");
            });

            return sb.ToString();
        }

        public static string GenerateVectorsCodeWithLocalSearch(string name, List<Vector3[]> allVectors)
        {
            indent = 0;
            sb = new StringBuilder();

            GenerateSwitchCases(name, null, (arrIndex) =>
            {
                Line("vectors = new Vector3[]");
                BracketsIn();
                {
                    for (int u = 0; u < allVectors[arrIndex].Length; u++)
                    {
                        Line(Vector3ToStr(allVectors[arrIndex][u]));
                    }
                }
                BracketsOut(true);
            });


            return sb.ToString();
        }

        private static void GenerateSwitchCases(string name, Action fillDistributionAction, Action<int> fillCaseAction)
        {
            SampleCount[] sampleCounts = GetSampleCountEntries();

            Line($"//Date of last modification: {DateTime.Now}");

            Line("using UnityEngine;");
            Space();
            Line($"namespace {nameof(PitGL)}");
            BracketsIn();
            {
                Line($"public partial class {nameof(VectorDistribution)}");
                BracketsIn();
                {
                    fillDistributionAction?.Invoke();

                    Line($"public static void CreateVectors_{name}({nameof(VectorDistributionParams)} distribution, SampleCount sampleCount, int patternRadius, out Vector3[] vectors)");
                    BracketsIn();
                    {
                     
                        Line("switch (sampleCount)");
                        BracketsIn();
                        {
                            Line("default:");
                            for (int sampleCountIndex = 0; sampleCountIndex < sampleCounts.Length; sampleCountIndex++)
                            {
                                SampleCount sampleCount = sampleCounts[sampleCountIndex];
                                Line($"case SampleCount.{sampleCount}:");
                                indent++;

                                int arrIndex = sampleCountIndex;

                                fillCaseAction?.Invoke(arrIndex);

                                Line("break;");
                                indent--;
                            }
                        }
                        BracketsOut();
                    }
                    BracketsOut();
                }
                BracketsOut();
            }
            BracketsOut();
        }

        private static void Space() => sb.Append("\r\n");
        private static void Append(string append) => sb.Append(append);
        private static void Line(string line)
        {
            Indent();
            sb.Append($"{line}\r\n");
        }
        private static void Indent()
        {
            for (int i = 0; i < indent; i++)
            {
                sb.Append("\t");
            }
        }
        private static void BracketsIn()
        {
            Line("{");
            indent++;
        }
        private static void BracketsOut(bool semicolon = false)
        {
            indent--;
            if (semicolon) Line("};");
            else Line("}");
        }

        private static string Vector4ToStr(Vector4 vector) => $"new Vector4({FloatToStr(vector.x)}f, {FloatToStr(vector.y)}f, {FloatToStr(vector.z)}f, {FloatToStr(vector.w)}f), ";
        private static string Vector3ToStr(Vector3 vector) => $"new Vector3({FloatToStr(vector.x)}f, {FloatToStr(vector.y)}f, {FloatToStr(vector.z)}f), ";
        private static string FloatToStr(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
#endif
    }
}
