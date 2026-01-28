using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Benchmarks.Benchmarks;

//BenchmarkRunner.Run<TestAlgorithm<LZO>>();

var config = ManualConfig.Create(DefaultConfig.Instance)
.AddColumn(new RatioColumn(), new SpeedColumn());
BenchmarkRunner.Run<TestAllAlgorithms>(config);
