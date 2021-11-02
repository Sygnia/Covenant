﻿// Author: Ryan Cobb (@cobbr_io)
// Project: Covenant (https://github.com/cobbr/Covenant)
// License: GNU GPLv3

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.CodeAnalysis;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Covenant.Core;

namespace Covenant.Models.Grunts
{
    public class GruntTask : IYamlSerializable<GruntTask>, ILoggable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), YamlIgnore]
        public int Id { get; set; }

        [YamlIgnore]
        public int AuthorId { get; set; }
        public GruntTaskAuthor Author { get; set; }

        [Required]
        public string Name { get; set; } = "GenericTask";
        public List<string> Aliases { get; set; } = new List<string>();
        public string Description { get; set; } = "A generic GruntTask.";
        public string Help { get; set; }
        public ImplantLanguage Language { get; set; } = ImplantLanguage.CSharp;
        public IList<Common.DotNetVersion> CompatibleDotNetVersions { get; set; } = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40 };

        public string Code { get; set; } = "";
        public bool Compiled { get; set; } = false;
        public GruntTaskingType TaskingType { get; set; } = GruntTaskingType.Assembly;

        public List<ReferenceSourceLibrary> ReferenceSourceLibraries { get; set; } = new List<ReferenceSourceLibrary>();
        public List<ReferenceAssembly> ReferenceAssemblies { get; set; } = new List<ReferenceAssembly>();
        public List<EmbeddedResource> EmbeddedResources { get; set; } = new List<EmbeddedResource>();

        public bool UnsafeCompile { get; set; } = false;
        public bool TokenTask { get; set; } = false;

        public List<GruntTaskOption> Options { get; set; } = new List<GruntTaskOption>();

        public string GetVerboseCommand(bool includeNotForDisplay = false)
        {
            string command = this.Name;
            for (int i = 0; i < this.Options.Count; i++)
            {
                if (this.Options[i].DisplayInCommand || includeNotForDisplay)
                {
                    command += " /" + this.Options[i].Name.ToLower() + ":\"" + this.Options[i].Value.Replace("\"", "\\\"") + "\"";
                }
            }
            return command;
        }

        public byte[] GetCompressedILAssembly35()
        {
            return File.ReadAllBytes(Common.CovenantTaskCSharpCompiledNet35Directory + this.Name + ".compiled");
        }

        public byte[] GetCompressedILAssembly40()
        {
            return File.ReadAllBytes(Common.CovenantTaskCSharpCompiledNet40Directory + this.Name + ".compiled");
        }

        public void Compile(ImplantTemplate template, Compiler.RuntimeIdentifier runtimeIdentifier = Compiler.RuntimeIdentifier.win_x64)
        {
            if (!this.Compiled)
            {
                foreach (Common.DotNetVersion version in template.CompatibleDotNetVersions.Intersect(this.CompatibleDotNetVersions))
                {
                    if (version == Common.DotNetVersion.Net35)
                    {
                        this.CompileDotNet35();
                    }
                    else if (version == Common.DotNetVersion.Net40)
                    {
                        this.CompileDotNet40();
                    }
                    else if (version == Common.DotNetVersion.Net50)
                    {
                        this.CompileDotNetCore(template, runtimeIdentifier);
                    }
                }
            }
        }

        private void CompileDotNet35()
        {
            List<Compiler.EmbeddedResource> resources = this.EmbeddedResources.Select(ER =>
            {
                return new Compiler.EmbeddedResource
                {
                    Name = ER.Name,
                    File = Common.CovenantEmbeddedResourcesDirectory + ER.Location,
                    Platform = Platform.X64,
                    Enabled = true
                };
            }).ToList();
            this.ReferenceSourceLibraries.ToList().ForEach(RSL =>
            {
                resources.AddRange(
                    RSL.EmbeddedResources.Select(ER =>
                    {
                        return new Compiler.EmbeddedResource
                        {
                            Name = ER.Name,
                            File = Common.CovenantEmbeddedResourcesDirectory + ER.Location,
                            Platform = Platform.X64,
                            Enabled = true
                        };
                    })
                );
            });
            List<Compiler.Reference> references35 = new List<Compiler.Reference>();
            this.ReferenceSourceLibraries.ToList().ForEach(RSL =>
            {
                references35.AddRange(
                    RSL.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net35).Select(RA =>
                    {
                        return new Compiler.Reference { File = Common.CovenantAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net35, Enabled = true };
                    })
                );
            });
            references35.AddRange(
                this.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net35).Select(RA =>
                {
                    return new Compiler.Reference { File = Common.CovenantAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net35, Enabled = true };
                })
            );
            
            File.WriteAllBytes(Common.CovenantTaskCSharpCompiledNet35Directory + this.Name + ".compiled",
                Utilities.Compress(Compiler.Compile(new Compiler.CsharpFrameworkCompilationRequest
                {
                    Language = this.Language,
                    Source = this.Code,
                    SourceDirectories = this.ReferenceSourceLibraries.Select(RSL => Common.CovenantReferenceSourceLibraries + RSL.Location).ToList(),
                    TargetDotNetVersion = Common.DotNetVersion.Net35,
                    References = references35,
                    EmbeddedResources = resources,
                    UnsafeCompile = this.UnsafeCompile,
                    Confuse = true,
                    // TODO: Fix optimization to work with Seatbelt
                    Optimize = !this.ReferenceSourceLibraries.Select(RSL => RSL.Name).Contains("Seatbelt")
                }))
            );
        }

        private void CompileDotNet40()
        {
            List<Compiler.EmbeddedResource> resources = this.EmbeddedResources.Select(ER =>
            {
                return new Compiler.EmbeddedResource
                {
                    Name = ER.Name,
                    File = Common.CovenantEmbeddedResourcesDirectory + ER.Location,
                    Platform = Platform.X64,
                    Enabled = true
                };
            }).ToList();
            this.ReferenceSourceLibraries.ToList().ForEach(RSL =>
            {
                resources.AddRange(
                    RSL.EmbeddedResources.Select(ER =>
                    {
                        return new Compiler.EmbeddedResource
                        {
                            Name = ER.Name,
                            File = Common.CovenantEmbeddedResourcesDirectory + ER.Location,
                            Platform = Platform.X64,
                            Enabled = true
                        };
                    })
                );
            });
            List<Compiler.Reference> references40 = new List<Compiler.Reference>();
            this.ReferenceSourceLibraries.ToList().ForEach(RSL =>
            {
                references40.AddRange(
                    RSL.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net40).Select(RA =>
                    {
                        return new Compiler.Reference { File = Common.CovenantAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net40, Enabled = true };
                    })
                );
            });
            references40.AddRange(
                this.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net40).Select(RA =>
                {
                    return new Compiler.Reference { File = Common.CovenantAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net40, Enabled = true };
                })
            );
            File.WriteAllBytes(Common.CovenantTaskCSharpCompiledNet40Directory + this.Name + ".compiled",
                Utilities.Compress(Compiler.Compile(new Compiler.CsharpFrameworkCompilationRequest
                {
                    Language = this.Language,
                    Source = this.Code,
                    SourceDirectories = this.ReferenceSourceLibraries.Select(RSL => Common.CovenantReferenceSourceLibraries + RSL.Location).ToList(),
                    TargetDotNetVersion = Common.DotNetVersion.Net40,
                    References = references40,
                    EmbeddedResources = resources,
                    UnsafeCompile = this.UnsafeCompile,
                    Confuse = true,
                    // TODO: Fix optimization to work with Seatbelt
                    Optimize = !this.ReferenceSourceLibraries.Select(RSL => RSL.Name).Contains("Seatbelt")
                }))
            );
        }

        private void CompileDotNetCore(ImplantTemplate template, Compiler.RuntimeIdentifier runtimeIdentifier)
        {
            string cspprojformat =
@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net50</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>

  {0}
</Project>";
            string referencegroupformat =
@"<ItemGroup>
    {0}
  </ItemGroup>";
            string referenceformat =
@"<Reference Include=""{0}"">
      <HintPath>{1}</HintPath>
    </Reference>";

            IEnumerable<string> references = this.ReferenceAssemblies.Select(RA =>
            {
                string name = RA.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? RA.Name.Substring(0, RA.Name.Length - 4) : RA.Name;
                return string.Format(referenceformat, name, RA.Location);
            });
            string csproj = string.Format(cspprojformat, string.Format(referencegroupformat, string.Join(Environment.NewLine + "    ", references)));
            string sanitizedName = Utilities.GetSanitizedFilename(template.Name);
            string dir = Common.CovenantImplantTemplateDirectory + sanitizedName + Path.DirectorySeparatorChar + "Task" + Path.DirectorySeparatorChar;
            string file = "Task" + Utilities.GetExtensionForLanguage(this.Language);
            File.WriteAllText(dir + "Task" + ".csproj", csproj);
            File.WriteAllText(dir + file, this.Code);
            File.WriteAllBytes(Common.CovenantTaskCSharpCompiledNet50Directory + this.Name + ".compiled",
                Utilities.Compress(Compiler.Compile(new Compiler.CsharpCoreCompilationRequest
                {
                    ResultName = "Task",
                    Language = this.Language,
                    TargetDotNetVersion = Common.DotNetVersion.Net50,
                    SourceDirectory = dir,
                    OutputKind = OutputKind.DynamicallyLinkedLibrary,
                    RuntimeIdentifier = runtimeIdentifier,
                    UseSubprocess = true
                }))
            );
        }

        // GruntTask|Action|ID|Name|Author|Aliases|Description|TaskingType|UnsafeCompile
        public string ToLog(LogAction action) => $"GruntTask|{action}|{this.Id}|{this.Name}|{this.Author}|{this.Aliases}|{this.Description}|{this.TaskingType}|{this.UnsafeCompile}";
    }
}
