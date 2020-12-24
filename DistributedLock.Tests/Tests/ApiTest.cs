﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text.RegularExpressions;

namespace Medallion.Threading.Tests
{
    [Category("CI")]
    public class ApiTest
    {
        private static object[] DistributedLockAssemblies => typeof(ApiTest).Assembly
            .GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("DistributedLock."))
            .ToArray<object>();

        [TestCaseSource(nameof(DistributedLockAssemblies))]
        public void TestPublicNamespaces(AssemblyName assemblyName)
        {
            var expectedNamespace = assemblyName.Name!.Replace("DistributedLock", "Medallion.Threading")
                .Replace(".Core", string.Empty);
            foreach (var type in GetPublicTypes(Assembly.Load(assemblyName)))
            {
                type.Namespace.ShouldEqual(expectedNamespace, $"{type} in {assemblyName}");
            }
        }

        [TestCaseSource(nameof(DistributedLockAssemblies))]
        public void TestPublicApisAreSealed(AssemblyName assemblyName)
        {
            foreach (var type in GetPublicTypes(Assembly.Load(assemblyName)).Where(t => t.IsClass))
            {
                if (!type.IsAbstract)
                {
                    Assert.IsTrue(type.IsSealed, $"{type} should be sealed");
                }
                else
                {
                    Assert.IsEmpty(
                        type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(c => c.IsPublic || c.Attributes.HasFlag(MethodAttributes.Family))
                    );
                }
            }
        }

        [TestCaseSource(nameof(DistributedLockAssemblies))]
        public void TestLibrariesUseConfigureAwaitFalse(AssemblyName assemblyName)
        {
            var projectDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(CurrentFilePath())!, "..", "..", assemblyName.Name!));
            var codeFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(codeFiles);

            var awaitRegex = new Regex(@"//.*|(?<await>\bawait\s)");
            var configureAwaitRegex = new Regex(@"\.ConfigureAwait\(false\)|\.TryAwait\(\)");
            foreach (var codeFile in codeFiles)
            {
                var code = File.ReadAllText(codeFile);
                var awaitCount = awaitRegex.Matches(code).Cast<Match>().Count(m => m.Groups["await"].Success);
                var configureAwaitCount = configureAwaitRegex.Matches(code).Count;
                Assert.IsTrue(configureAwaitCount >= awaitCount, $"ConfigureAwait(false) count ({configureAwaitCount}) < await count ({awaitCount}) in {codeFile}");
            }
        }

        [Test]
        public void TestLibraryFilesDoNotWriteToConsole()
        {
            var projectDirectory = Path.GetDirectoryName(Path.GetDirectoryName(CurrentFilePath()));
            var solutionDirectory = Path.GetDirectoryName(projectDirectory!);
            var libraryCsFiles = Directory.GetFiles(solutionDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(f => new[] { ".Tests", "CodeGen", "DistributedLockTaker" }.All(s => f.IndexOf(s, StringComparison.OrdinalIgnoreCase) < 0));
            Assert.IsEmpty(
                libraryCsFiles.Where(f => File.ReadAllText(f).Contains("Console."))
                    .Select(Path.GetFileName)
            );
        }

        private static IEnumerable<Type> GetPublicTypes(Assembly assembly) => assembly.GetTypes()
                .Where(IsInPublicApi)
#if DEBUG
                .Where(t => !(t.Namespace!.Contains(".Internal") && assembly.GetName().Name == "DistributedLock.Core"))
#endif
            ;

        private static string CurrentFilePath([CallerFilePath] string filePath = "") => filePath;

        private static bool IsInPublicApi(Type type) => type.IsPublic
            || ((type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem) && IsInPublicApi(type.DeclaringType!));
    }
}
