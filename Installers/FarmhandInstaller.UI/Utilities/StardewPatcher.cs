﻿namespace FarmhandInstaller.UI.Utilities
{
    using System;
    using System.IO;
    using System.Reflection;

    using Farmhand;

    internal static class StardewPatcher
    {
        private class PatcherProxy : MarshalByRefObject
        {
            private dynamic Patcher { get; set; }

            public void Initialize(string assemblyPath, string type)
            {
                var test = Assembly.UnsafeLoadFrom(assemblyPath);
                var patcherType = test.GetType(type);
                this.Patcher = Activator.CreateInstance(patcherType);
            }

            public void LoadCommon(string assemblyDirectory)
            {
                Assembly.UnsafeLoadFrom(Path.Combine(assemblyDirectory, "ILRepack.dll"));
                Assembly.UnsafeLoadFrom(Path.Combine(assemblyDirectory, "FarmhandPatcherCommon.dll"));
                Assembly.UnsafeLoadFrom(Path.Combine(assemblyDirectory, "Farmhand.dll"));
            }

            public void SetOptions(string assemblyDirectory, bool disableGrm = true)
            {
                this.Patcher.Options.DisableGrm = disableGrm;
                this.Patcher.Options.AssemblyDirectory = assemblyDirectory;
            }

            public void Patch(string path)
            {
                this.Patcher.PatchStardew(path);
            }
        }

        private static PatcherProxy CreatePatcher(StardewPatcherPass pass, string assemblyLocation, AppDomain domain)
        {
            var type = typeof(PatcherProxy);
            var proxy = (PatcherProxy)domain.CreateInstanceAndUnwrap(
                type.Assembly.FullName,
                type.FullName);
                
            proxy.Initialize(
                pass == StardewPatcherPass.PassOne
                    ? Path.Combine(assemblyLocation, "FarmhandPatcherFirstPass.dll")
                    : Path.Combine(assemblyLocation, "FarmhandPatcherSecondPass.dll"),
                pass == StardewPatcherPass.PassOne ? "Farmhand.PatcherFirstPass" : "Farmhand.PatcherSecondPass");
            return proxy;
        }

        public static void Patch(string outputPath, string assemblyDirectory, bool disableGrm, PackageStatusContext context)
        {
            var localDomain = AppDomain.CreateDomain("LocalDomain");
            localDomain.AssemblyResolve += LocalDomain_AssemblyResolve;

            var sdvPath = Path.Combine(InstallationContext.StardewPath, "Stardew Valley.exe");

            context.SetState(60, "Creating First Pass Patcher");
            var patcher = CreatePatcher(StardewPatcherPass.PassOne, assemblyDirectory, localDomain);
            context.SetState(65, "Loading Common Assemblies");
            patcher.LoadCommon(assemblyDirectory);
            patcher.SetOptions(assemblyDirectory, disableGrm);
            context.SetState(70, "Patching Intermediate Executable");
            patcher.Patch(sdvPath);

            context.SetState(75, "Creating Second Pass Patcher");
            patcher = CreatePatcher(StardewPatcherPass.PassTwo, assemblyDirectory, localDomain);
            patcher.LoadCommon(assemblyDirectory);
            patcher.SetOptions(assemblyDirectory, disableGrm);
            context.SetState(80, "Patching Final Executable");
            patcher.Patch(outputPath);

            AppDomain.Unload(localDomain);
        }
        
        private static Assembly LocalDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assembly.GetExecutingAssembly();
        }
    }
}