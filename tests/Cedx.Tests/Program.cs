using Cedx.Core.Models;
using Cedx.Core.Parsing;
using Cedx.Core.Services;
int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
var parser=new AssetTextParser();
var report="""
Hostname: LAB-01
IP Address: 192.0.2.10
Windows account: LAB\operator
OS Version: Microsoft Windows 11 Pro
AnyDesk ID: 123456789
=== Local account details ===
Name : operator
Enabled : True
Description : a long description
              continued here

Name : service
Enabled : False
=== Office vNext diagnostic (original user) ===
========== vNext licenses found ==========
No licenses found
=== Windows license keys ===
OEM : AAAAA-BBBBB-CCCCC-DDDDD-EEEEE
=== Collector details ===
Warning : Access is denied.
""";
var a=parser.Parse(report,"lab.txt",DateTimeOffset.UtcNow);
Check(a.DetailSections.Any(s=>s.Title=="Local account details"),"Extended section");
var accounts=a.DetailSections.Single(s=>s.Title=="Local account details");
Check(accounts.Rows.Count(r=>r.Field=="Name")==2,"Multiple records");
Check(accounts.Rows.Any(r=>r.Value.Contains("continued here")),"Wrapped value");
Check(a.DetailSections.Single(s=>s.Title.StartsWith("Office vNext")).RawContent.Contains("No licenses found"),"Nested banner");
Check(new AssetQuery{Search="operator \"Access is denied\""}.Matches(a),"AND phrase search");
Check(!new AssetQuery{Search="operator missingterm"}.Matches(a),"AND excludes");
Check(!a.SearchIndex.Contains("AAAAA-BBBBB"),"Keys excluded from search");
Check(AssetDetails.RedactKeys(report).Contains("[key hidden]"),"Key masking");
Check(new AssetQuery().Matches(a),"Unknown measurements included");
Check(!new AssetQuery{IncludeUnknown=false}.Matches(a),"Unknown measurements excluded");
Check(!new AssetQuery{MinRam=10,MaxRam=5}.Matches(a),"Invalid range");
Check(new AssetQuery{MinRam=double.NaN}.Validation.Length>0,"Nonfinite range");
Check(a.HasAnyDesk,"Valid remote ID");
a.Network.AnyDeskId="Not installed";Check(!a.HasAnyDesk,"Invalid remote ID");
a.LocalDisks=[new(){DriveLetter="C:",FreeGb=20}];a.LowStorageThresholdGb=30;Check(a.HasLowStorage,"Configurable threshold");
var notified=false;a.PropertyChanged+=(_,e)=>notified|=e.PropertyName==nameof(a.OnlineStatus);a.OnlineStatus=ScanStatus.Online;Check(notified,"Live status notification");
Check(parser.Parse("Hostname: OLD-PC","old.txt",DateTimeOffset.UtcNow).DetailSections.Count>0,"Legacy sparse report");
if(args.Length>0){var real=parser.Parse(File.ReadAllText(args[0]),args[0],DateTimeOffset.UtcNow);Check(real.DetailSections.Count>30,"Real report sections");Console.WriteLine($"Local report: {real.DetailSections.Count} sections, {real.DetailSections.Sum(s=>s.Rows.Count)} fields");}
Console.WriteLine($"PASS: {checks} checks");
