//https://blog.demofox.org/2022/01/01/interleaved-gradient-noise-a-different-kind-of-low-discrepancy-sequence/
#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System;
using System.Threading;
using Architecture;

namespace PitGL
{
    public class VectorDistributionOptimizer : MonoBehaviour
    {
        public string distributionName = "SSAO";
        public string distributionFolder = "Assets/Scripts/Rendering/Runtime/VectorDistribution";
        public bool autoGenerateCode = false;

        [Header("PARAMETERS")]
        [SerializeField, RangeEnum] VectorDistribution.SampleCount sampleCount = VectorDistribution.SampleCount.x4;
        [SerializeField] int seed = 6208;

        [System.NonSerialized] public int lastSeed;
        [System.NonSerialized] public Vector3[] vectors;
        [System.NonSerialized] public List<Vector3[]> allVectors;
        [System.NonSerialized] private Vector3[] combinedVectors;
        [System.NonSerialized] public List<int> bestSeeds;

        [SerializeField] VectorDistributionParams distribution;


        [Header("SEARCH ALGORITHM")]
        public float weightOfMinDistanceBetweenPoints = 1f;
        public float weightOfSignificantSamples = 0f;

        [Header("Grasp")]
        [SerializeField] public int maxSeeds = 10000;
        [SerializeField] public float maxGraspSeconds = 60f;
        [SerializeField, Range(0.01f, 1f)] public float graspConvergencePct = 0.1f;
        [SerializeField, Range(0, 32)] public int threads = 0;

        [Header("Local Search")]
        [SerializeField] public bool performLocalSearch = false;
        [SerializeField] public int promisingSeeds = 100;
        [SerializeField] public int localSearchIters = 10000;
        [SerializeField] public float maxLocalSearchSeconds = 60f;
        [SerializeField, Range(0.01f, 1f)] public float lsConvergencePct = 0.3f;

        [SerializeField] public Vector2 pointPerturbationDistRange = new Vector2(0.01f, 0.1f);
        [SerializeField] public Vector2 anglePerturbationRange = new Vector2(1f, 4f);
        [SerializeField] public Vector2 lengthPerturbationRange = new Vector2(0.02f, 0.5f);
        [SerializeField] public Vector2 forwardPerturbationRange = new Vector2(0.02f, 0.5f);

        public bool HasComputedVectors => vectors != null && vectors.Length > 0 && lastSeed == seed;


#if UNITY_EDITOR
        public void FillCombinedVectors()
        {
            if (HasComputedVectors && performLocalSearch)
            {
                for(int i = 0; i < vectors.Length; i++)
                {
                    vectors[i] = combinedVectors[i];
                }
            }
            else
            {
                combinedVectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, seed);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (UnityEditor.Selection.activeGameObject != gameObject) return;
            //float[] vectors = Util.NormalizeVectorsAndPutMagnitudeInVectorW(Util.WellDistributedPoissonVectors(Util.PoissonVectors[0], sampleCount));
            Gizmos.color = Color.yellow;
            FillCombinedVectors();

            int sampleCountInt = (int)sampleCount;
            DrawVectorsAt(0, 0, 0, sampleCountInt);

            void DrawVectorsAt(int x, int y, int start, int count)
            {
                Vector3 center = new Vector3(x, 0, y) * 2;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(1, 0, 1));
                //Gizmos.DrawWireCube(center, new Vector3(2f, 0f, 2f));
                DrawWireCircle(center);

                Gizmos.matrix = transform.localToWorldMatrix;
                for (int i = 0; i < count; i++)
                {
                    Vector3 vector = combinedVectors[start + i];
                    Gizmos.DrawLine(center, center + vector);
                    Gizmos.DrawSphere(center + vector, 0.04f);
                }
            }

            void DrawWireCircle(Vector3 center)
            {
                const int resolution = 32;
                Vector3 src = Vector3.right;
                Quaternion rot = Quaternion.AngleAxis(360f / (resolution), Vector3.up);

                for(int i = 0; i < resolution; i++)
                {
                    Vector3 dst = rot * src;
                    Gizmos.DrawLine(src + center, dst + center);
                    src = dst;
                }
            }

            
        }

        [CustomEditor(typeof(VectorDistributionOptimizer))]
        private class PoissonDistributionGeneratorEditor : Editor
        {
            private static volatile bool canceled = false;
            private static volatile int currentSeed = 0;
            private static volatile int bestSeed = 0;
            private static volatile float maxDistance = 0;
            private static volatile int maxSeeds = 0;
            private static volatile float convergenceMaxTime = 0;
            private static DateTime convergenceStart;
            private static volatile (int, float)[] promisingSeeds;
            private static volatile Vector3[] searchedVectors = null;
            private static volatile CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            private static volatile float weightOfMinDistanceBetweenPoints;
            private static volatile float weightOfSignificantSamples;

            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                serializedObject.Update();


                VectorDistributionOptimizer optimizer = (VectorDistributionOptimizer)target;

                GUILayout.Space(12);

                GUILayout.Label("THIS CONFIGURATION ACTIONS", EditorStyles.boldLabel);
                if (GUILayout.Button("EVALUATE MIN DISTANCE METRIC"))
                {
                    SetWeightConfig(optimizer);
                    optimizer.FillCombinedVectors();
                    float minDistance = EvaluateMinDistanceMetric(optimizer.combinedVectors);
                    Debug.Log($"Min Distance: {minDistance}");
                }
                
                if (GUILayout.Button("FIND A GOOD SEED FOR THIS CONFIGURATION"))
                {
                    canceled = false;
                    int bestSeed = MaximizeMinDistanceForVectors(optimizer.sampleCount, optimizer, true, out optimizer.vectors);
                    Debug.Log($"Best Seed: {bestSeed} with distance {maxDistance}");
                    serializedObject.FindProperty("seed").intValue = bestSeed;
                    optimizer.lastSeed = bestSeed;
                }
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("PERFORM LOCAL SEARCH"))
                    {
                        canceled = false;
                        SetWeightConfig(optimizer);
                        float foundMaxMinDistance = LocalSearch(optimizer, optimizer.sampleCount, optimizer.seed, 0, 1, true, out optimizer.vectors);
                        EditorUtility.ClearProgressBar();
                        Debug.Log($"Found a feashible vector set of min distance: {foundMaxMinDistance}");
                        optimizer.lastSeed = optimizer.seed;
                    }
                    if (GUILayout.Button("UNDO LOCAL SEARCH"))
                    {
                        optimizer.vectors = null;
                    }
                }

                if(GUILayout.Button("PRINT VECTORS"))
                {
                    for(int i = 0; i < optimizer.vectors.Length; i++)
                    {
                        Debug.Log(optimizer.vectors[i]);
                    }
                }

                GUILayout.Space(20);

                GUILayout.Label("EVERY CONFIGURATION ACTIONS", EditorStyles.boldLabel);

                int totalExpectedThreads = optimizer.threads + 1;
                if (totalExpectedThreads < 0) totalExpectedThreads = 0;
                int effectiveThreads = Environment.ProcessorCount < totalExpectedThreads ? Environment.ProcessorCount : totalExpectedThreads;

                float estimatedSeconds = optimizer.maxGraspSeconds;
                if (optimizer.performLocalSearch) estimatedSeconds += (optimizer.maxLocalSearchSeconds * optimizer.promisingSeeds) / effectiveThreads;
                estimatedSeconds *= VectorDistribution.MAX_SAMPLE_COUNT_VARIANTS;

                TimeSpan estimatedTime = TimeSpan.FromSeconds(estimatedSeconds);

                if (GUILayout.Button($"FIND A GOOD SEED FOR EVERY CONFIGURATION (Time: {estimatedTime.ToString(@"dd\:hh\:mm\:ss")})"))
                {
                    FindGoodSeedsForAllConfigurations(optimizer, out optimizer.allVectors, out optimizer.bestSeeds);
                    if (optimizer.autoGenerateCode) GenerateCodeInPath();
                    
                }

                if (GUILayout.Button($"GENERATE CODE IN CLIPBOARD"))
                {
                    string code = optimizer.performLocalSearch ? VectorDistribution.GenerateVectorsCodeWithLocalSearch(optimizer.distributionName, optimizer.allVectors) :
                        VectorDistribution.GenerateVectorsCodeWithSeeds(optimizer.distributionName, optimizer.distribution, optimizer.bestSeeds);
                    Debug.Log(code);
                    GUIUtility.systemCopyBuffer = code;
                }

                if (GUILayout.Button($"GENERATE CODE IN PATH"))
                {
                    GenerateCodeInPath();
                }

                GUILayout.Space(20);
                

                serializedObject.ApplyModifiedProperties();

                void GenerateCodeInPath()
                {
                    string code = optimizer.performLocalSearch ? VectorDistribution.GenerateVectorsCodeWithLocalSearch(optimizer.distributionName, optimizer.allVectors) :
                        VectorDistribution.GenerateVectorsCodeWithSeeds(optimizer.distributionName, optimizer.distribution, optimizer.bestSeeds);
                    Debug.Log(code);
                    string filePath = $"{optimizer.distributionFolder}/{nameof(VectorDistribution)}_{optimizer.distributionName}.cs";
                    System.IO.File.WriteAllText(filePath, code);
                    AssetDatabase.Refresh();
                }

            }

            private void SetWeightConfig(VectorDistributionOptimizer optimizer)
            {
                weightOfMinDistanceBetweenPoints = optimizer.weightOfMinDistanceBetweenPoints;
                weightOfSignificantSamples = optimizer.weightOfSignificantSamples;
            }

            private void FindGoodSeedsForAllConfigurations(VectorDistributionOptimizer optimizer, out List<Vector3[]> allVectors, out List<int> allSeeds)
            {
                canceled = false;

                allVectors = new List<Vector3[]>();
                allSeeds = new List<int>();

                VectorDistribution.SampleCount[] sampleCounts = VectorDistribution.GetSampleCountEntries();

                for (int sampleCountIndex = 0; sampleCountIndex < VectorDistribution.MAX_SAMPLE_COUNT_VARIANTS && !canceled; sampleCountIndex++)
                {
                    VectorDistribution.SampleCount sampleCount = sampleCounts[sampleCountIndex];
                    int bestSeed = MaximizeMinDistanceForVectors(sampleCount, optimizer, false, out Vector3[] vectors);
                    Debug.Log($"Best seed for sample count = {sampleCount} is {bestSeed} with maximized distance: {maxDistance}");

                    allVectors.Add(vectors);
                    allSeeds.Add(bestSeed);

                    if (sampleCount == optimizer.sampleCount)
                    {
                        optimizer.vectors = vectors;
                        optimizer.lastSeed = bestSeed;
                        serializedObject.FindProperty("seed").intValue = bestSeed;
                    }

                }
            }

            #region Maximization

            private int MaximizeMinDistanceForVectors(VectorDistribution.SampleCount sampleCount, VectorDistributionOptimizer optimizer, bool log, out Vector3[] bestVectors)
            {
                bestVectors = null;

                if (sampleCount == VectorDistribution.SampleCount.x1)
                {
                    bestVectors = new Vector3[] { new Vector3(0, 0.4f, 0f) };
                    return 0;
                }

                
                // Thread shared variables
                bestSeed = 0;
                maxDistance = float.MinValue;
                maxSeeds = optimizer.maxSeeds;
                convergenceMaxTime = optimizer.graspConvergencePct * optimizer.maxGraspSeconds;
                convergenceStart = DateTime.Now;
                promisingSeeds = new (int, float)[optimizer.promisingSeeds];
                SetWeightConfig(optimizer);
                for (int i = 0; i < promisingSeeds.Length; i++)
                {
                    promisingSeeds[i] = (-1, float.MinValue);
                }
                //

                float maxSeconds = optimizer.maxGraspSeconds;
                DateTime now = DateTime.Now;
                float elapsedSeconds = 0f;

                int maxSecondsInt = Mathf.RoundToInt(maxSeconds);


                string title = $"Maximizing min distance for sampleCount = {sampleCount}";
                canceled = false;
                canceled = EditorUtility.DisplayCancelableProgressBar(title, $"Starting", 0f);
                currentSeed = 0;

                Mutex mutex = new Mutex(false);

                Thread[] threads = new Thread[optimizer.threads];
                cancellationTokenSource = new CancellationTokenSource();
                CancellationToken token = cancellationTokenSource.Token;

                for (int t = 0; t < optimizer.threads; t++)
                {
                    int id = t + 1;
                    threads[t] = new Thread(() => GraspSeedsMultithreaded(false, id, token));
                    threads[t].Start();
                }

                GraspSeedsMultithreaded(true, 0, token);

                if(canceled) cancellationTokenSource.Cancel();
                for (int t = 0; t < optimizer.threads; t++) threads[t].Join();

                cancellationTokenSource.Dispose();
                EditorUtility.ClearProgressBar();
                
                //LOCAL SEARCH
                if(!canceled && optimizer.performLocalSearch && !(sampleCount == VectorDistribution.SampleCount.x1))
                {
                    Debug.Log($"Before local search, best seed is {bestSeed} with min distance: {maxDistance}");
                    title = $"Local searching for sampleCount = {sampleCount}";

                    cancellationTokenSource = new CancellationTokenSource();
                    token = cancellationTokenSource.Token;
                    currentSeed = 0;
                    maxDistance = float.MinValue;
                    searchedVectors = null;
                    mutex = new Mutex(false);

                    int countThreads = optimizer.promisingSeeds - 1;
                    if (optimizer.threads < countThreads) countThreads = optimizer.threads;
                    threads = new Thread[optimizer.threads];
                    for (int t = 0; t < optimizer.threads; t++)
                    {
                        int id = t + 1;
                        threads[t] = new Thread(() => LocalSearchMultiThreaded(false, id, token));
                        threads[t].Start();
                    }

                    LocalSearchMultiThreaded(true, 0, token);

                    if (canceled) cancellationTokenSource.Cancel();

                    bool anyAlive = true;
                    while(anyAlive)
                    {
                        int countAlive = 0;
                        anyAlive = false;
                        for (int t = 0; t < optimizer.threads; t++)
                        {
                            if (threads[t].IsAlive)
                            {
                                countAlive++;
                                anyAlive = true;
                            }
                        }
                        int pending = optimizer.threads - countAlive;
                        EditorUtility.DisplayProgressBar($"Local search for sample count = {sampleCount}", $"Waiting for {pending} threads to finish", (float)pending / (float)optimizer.threads);
                        if (anyAlive)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    

                    for (int t = 0; t < optimizer.threads; t++)
                    {
                        threads[t].Join();
                    }

                    cancellationTokenSource.Dispose();
                    EditorUtility.ClearProgressBar();

                    bestVectors = searchedVectors;
                }
                else
                {
                    bestVectors = VectorDistributionGenerator.GenerateVectors(optimizer.distribution, sampleCount, bestSeed);
                }

                if (log)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Promising seeds: ");
                    for(int i = 0; i < promisingSeeds.Length; i++)
                    {
                        sb.AppendLine($"seed {promisingSeeds[i].Item1} with min distance: {promisingSeeds[i].Item2}");
                    }
                    Debug.Log(sb.ToString());   
                }

                return bestSeed;

                #region Multithreaded

                void GraspSeedsMultithreaded(bool mainThread, object idObj, CancellationToken token)
                {
                    int id = (int)idObj;

                    string finishMsg = "";

                    while(true)
                    {
                        if (token.IsCancellationRequested)
                        {
                            finishMsg = "token cancellation requested";
                            break;
                        }

                        int seed;

                        mutex.WaitOne();
                        {
                            if (currentSeed >= maxSeeds || canceled)
                            {
                                finishMsg = $"current seed: {currentSeed}/{maxSeeds}, canceled: {canceled}";
                                mutex.ReleaseMutex();
                                break;
                            }

                            seed = currentSeed;
                            currentSeed++;
                        }
                        mutex.ReleaseMutex();

                        if(mainThread)
                        {
                            elapsedSeconds = ((float)(DateTime.Now - now).TotalSeconds);
                            float timeProgress = elapsedSeconds / maxSeconds;
                            float realProgress = (float)seed / (float)maxSeeds;
                            float displayProgress = Mathf.Max(realProgress, timeProgress);

                            mutex.WaitOne();
                            float convergenceElapsed = (float)(DateTime.Now - convergenceStart).TotalSeconds;
                            canceled = EditorUtility.DisplayCancelableProgressBar(title,
                                $"Seed: {seed}, Time: {Mathf.RoundToInt(elapsedSeconds)}/{maxSecondsInt}, Max Min Dist: {maxDistance}, Converged: {(convergenceElapsed * 100f / convergenceMaxTime).ToString("0.00")}%", displayProgress);
                            bool localCanceled = canceled;
                            mutex.ReleaseMutex();

                            if (localCanceled)
                            {
                                Debug.Log($"[{id}] Cancelling manually");
                                cancellationTokenSource.Cancel();
                                break;
                            }
                            else if (elapsedSeconds >= maxSeconds)
                            {
                                cancellationTokenSource.Cancel();
                                Debug.Log($"[{id}] Cancelling because of time out, {elapsedSeconds}/{maxSeconds}");
                                break;
                            }
                        }

                        Vector3[] combinedVectors = VectorDistributionGenerator.GenerateVectors(optimizer.distribution, sampleCount, seed);
                        float seedMinDistance = EvaluateMinDistanceMetric(combinedVectors);

                        mutex.WaitOne();
                        TrySetPromisingSeed(seed, seedMinDistance);
                        if (seedMinDistance > maxDistance)
                        {
                            maxDistance = seedMinDistance;
                            bestSeed = seed;
                            convergenceStart = DateTime.Now;
                            mutex.ReleaseMutex();
                            if (log) Debug.Log($"[{id}] Found a better seed ({bestSeed}) with min distance {maxDistance}");
                        }
                        else
                        {
                            float convergenceElapsed = ((float)(DateTime.Now - convergenceStart).TotalSeconds);
                            mutex.ReleaseMutex();

                            if (convergenceElapsed >= convergenceMaxTime)
                            {
                                cancellationTokenSource.Cancel();
                                finishMsg = $"[{id}] Converged after {convergenceElapsed} seconds without improvement";
                                break;
                            }
                        }
                    }

                    Debug.Log($"[{id}] Finished ({finishMsg})");

                    void TrySetPromisingSeed(int seed, float minDistance)
                    {
                        for (int i = 0; i < promisingSeeds.Length; i++)
                        {
                            if (minDistance > promisingSeeds[i].Item2)
                            {
                                promisingSeeds[i].Item1 = seed;
                                promisingSeeds[i].Item2 = minDistance;
                                break;
                            }
                        }
                    }
                }

                void LocalSearchMultiThreaded(bool mainThread, int id, CancellationToken token)
                {
                    string finishMsg = "";

                    while(true)
                    {
                        if (token.IsCancellationRequested)
                        {
                            finishMsg = "token cancellation requested";
                            break;
                        }

                        mutex.WaitOne();
                        int promisingIndex = currentSeed;
                        currentSeed++;

                        if(currentSeed >= promisingSeeds.Length || canceled)
                        {
                            finishMsg = canceled ? "cancelled" : "no more seeds";
                            mutex.ReleaseMutex();
                            break;
                        }

                        mutex.ReleaseMutex();

                        (int seed, float minDistance) = promisingSeeds[promisingIndex];

                        if (seed < 0) continue;

                        float previousMinDistance = minDistance;
                        minDistance = LocalSearch(optimizer, sampleCount, seed, promisingIndex, promisingSeeds.Length, mainThread, out Vector3[] improvedVectors);
                        promisingSeeds[promisingIndex].Item2 = minDistance;

                        if(log)
                        {
                            Debug.Log($"[{id}] Improved seed {seed} from min distance {previousMinDistance} to {minDistance}");
                        }

                        mutex.WaitOne();
                        if(minDistance > maxDistance)
                        {
                            maxDistance = minDistance;
                            bestSeed = seed;
                            searchedVectors = improvedVectors;
                        }
                        mutex.ReleaseMutex();
                    
                    }

                    Debug.Log($"[{id}] Finished ({finishMsg})");
                }

                #endregion
            }

            #endregion

            #region Local Search

            private float LocalSearch(VectorDistributionOptimizer optimizer, VectorDistribution.SampleCount sampleCount, int seed, int index, int maxIndex, bool mainThread, out Vector3[] improvedVectors)
            {
                System.Random rnd = new System.Random(seed);
                float pointPerturbationDist = Util.RandomRange(rnd, optimizer.pointPerturbationDistRange.x, optimizer.pointPerturbationDistRange.y);
                float anglePerturbation = Util.RandomRange(rnd, optimizer.anglePerturbationRange.x, optimizer.anglePerturbationRange.y);
                float lengthPerturbation = Util.RandomRange(rnd, optimizer.lengthPerturbationRange.x, optimizer.lengthPerturbationRange.y);
                float forwardPerturbation = Util.RandomRange(rnd, optimizer.forwardPerturbationRange.x, optimizer.forwardPerturbationRange.y);

                VectorDistributionParams distribution = optimizer.distribution;

                Vector3[] vectors = VectorDistributionGenerator.GenerateVectors(distribution, sampleCount, seed);

                float maxMinDistance = EvaluateMinDistanceMetric(vectors);
                string title = $"Local search {index}/{maxIndex} for sampleCount = {sampleCount}";
                int maxSecondsInt = Mathf.RoundToInt(optimizer.maxLocalSearchSeconds);

                DateTime now = DateTime.Now;
                float elapsedSeconds = 0f;
                convergenceStart = now;
                convergenceMaxTime = optimizer.lsConvergencePct * optimizer.maxLocalSearchSeconds;
                float convergenceElapsed = 0f;

                for (int i = 0; i < optimizer.localSearchIters && !canceled && elapsedSeconds < optimizer.maxLocalSearchSeconds; i++)
                {
                    bool modified = false;
                    modified |= VectorDistributionGenerator.TryPerturbPoints(distribution, rnd, vectors, pointPerturbationDist, out int vecIdx, out Vector3 previousVector);

                    if (modified)
                    {
                        float newMaxMinDistance = EvaluateMinDistanceMetric(vectors);
                        if (newMaxMinDistance > maxMinDistance)
                        {
                            maxMinDistance = newMaxMinDistance;
                            convergenceStart = DateTime.Now;
                        }
                        else
                        {
                            //The new perturbation is worse that the previous result, we must undo the changes
                            vectors[vecIdx] = previousVector;


                            convergenceElapsed = ((float)(DateTime.Now - convergenceStart).TotalSeconds);
                            if(convergenceElapsed >= convergenceMaxTime)
                            {
                                break;
                            }
                        }
                    }

                    elapsedSeconds = ((float)(DateTime.Now - now).TotalSeconds);
                    float timeProgress = elapsedSeconds / optimizer.maxLocalSearchSeconds;
                    float realProgress = (float)i / (float)optimizer.localSearchIters;
                    float displayProgress = Mathf.Max(realProgress, timeProgress);
                    float globalProgress = (float)index / (float)maxIndex + displayProgress / maxIndex;

                    if (mainThread)
                    {
                        canceled = EditorUtility.DisplayCancelableProgressBar(title,
                                $"Seed: {seed}, Time: {Mathf.RoundToInt(elapsedSeconds)}/{maxSecondsInt}, Max Min Dist: {maxMinDistance}, " +
                                $"Converged: {(convergenceElapsed * 100f / convergenceMaxTime).ToString("0.00")}%", globalProgress);
                    }
                }

                //if (mainThread)
                //{
                //    EditorUtility.ClearProgressBar();
                //}
                

                improvedVectors = vectors;
                return maxMinDistance;
            }

            #endregion

            #region Metric

            private float EvaluateMinDistanceMetric(Vector3[] vectors)
            {
                float minDistance = float.MaxValue;
                float significantSamples = 0f;

                for(int i = 0; i < vectors.Length; i++)
                {
                    for(int j = i+1; j < vectors.Length; j++)
                    {
                        float dist = Vector3.SqrMagnitude(vectors[i] - vectors[j]);
                        if(dist < minDistance)
                        {
                            minDistance = dist;
                        }
                    }

                    float magnitude = vectors[i].magnitude;
                    significantSamples += 1f - magnitude;
                }


                minDistance = Mathf.Sqrt(minDistance);
                significantSamples /= vectors.Length;

                minDistance = Mathf.Lerp(1, minDistance, weightOfMinDistanceBetweenPoints);
                minDistance *= Mathf.Lerp(1f, significantSamples, weightOfSignificantSamples);

                return minDistance;
            }

            #endregion
        }

#endif
    }
}
