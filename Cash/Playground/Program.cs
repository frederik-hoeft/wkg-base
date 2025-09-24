
using BenchmarkDotNet.Running;
using Playground;

Environment.SetEnvironmentVariable("R_HOME", @"E:\software\R-4.3.2");
BenchmarkRunner.Run<ValueStore>();
Console.ReadLine();
