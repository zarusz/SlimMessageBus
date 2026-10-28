param(
    [string] $InputPath = './coverage',
    [string] $OutputPath = './coverage-sonar'
)

$ErrorActionPreference = 'Stop'
$inputRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($InputPath))
$outputRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($OutputPath))
if ($inputRoot -eq $outputRoot -or $outputRoot.StartsWith($inputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Sonar import copies must be separate from the raw coverage artifacts.'
}
$reports = @(Get-ChildItem -LiteralPath $inputRoot -Filter '*.opencover.xml' -File -Recurse | Sort-Object FullName)
if (!$reports.Count) { throw 'No OpenCover reports were found.' }
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
if (Get-ChildItem -LiteralPath $outputRoot -Filter '*.opencover.xml' -File -Recurse) {
    throw 'The Sonar coverage output directory must not contain old reports.'
}

$readerSettings = [System.Xml.XmlReaderSettings]::new()
$readerSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$readerSettings.XmlResolver = $null
$removedPoints = 0

foreach ($report in $reports) {
    $relativePath = [IO.Path]::GetRelativePath($inputRoot, $report.FullName)
    $reader = [System.Xml.XmlReader]::Create($report.FullName, $readerSettings)
    $document = [System.Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    try { $document.Load($reader) }
    catch { throw "Cannot parse coverage report '$relativePath': $($_.Exception.Message)" }
    finally { $reader.Dispose() }

    foreach ($module in $document.SelectNodes('/CoverageSession/Modules/Module')) {
        # OpenCover file IDs are scoped to the module. Never infer a replacement
        # source file from a class name or trim points based on source line counts.
        $generatedIds = [System.Collections.Generic.HashSet[string]]::new()
        foreach ($file in $module.SelectNodes('Files/File')) {
            $path = $file.GetAttribute('fullPath').Replace('\', '/')
            if ($path -match '(^|/)obj/|\.g(\.i)?\.cs$') {
                [void] $generatedIds.Add($file.GetAttribute('uid'))
            }
        }
        if (!$generatedIds.Count) { continue }

        foreach ($method in $module.SelectNodes('Classes/Class/Methods/Method')) {
            $fileRef = $method.SelectSingleNode('FileRef')
            $defaultId = if ($null -ne $fileRef) { $fileRef.GetAttribute('uid') } else { '' }
            foreach ($point in @($method.SelectNodes('SequencePoints/SequencePoint | BranchPoints/BranchPoint | MethodPoint'))) {
                $fileId = if ($point.HasAttribute('fileid')) { $point.GetAttribute('fileid') } else { $defaultId }
                if ($generatedIds.Contains($fileId)) {
                    [void] $point.ParentNode.RemoveChild($point)
                    $removedPoints++
                }
            }
            if ($generatedIds.Contains($defaultId)) {
                $remainingPoint = $method.SelectSingleNode('SequencePoints/SequencePoint[@fileid] | BranchPoints/BranchPoint[@fileid]')
                if ($null -ne $remainingPoint) {
                    $fileRef.SetAttribute('uid', $remainingPoint.GetAttribute('fileid'))
                }
                else { [void] $fileRef.ParentNode.RemoveChild($fileRef) }
            }
        }
        foreach ($file in @($module.SelectNodes('Files/File'))) {
            if ($generatedIds.Contains($file.GetAttribute('uid'))) {
                [void] $file.ParentNode.RemoveChild($file)
            }
        }
    }

    # Sonar imports sequence/branch points, not OpenCover's summary percentages.
    # Keep the untouched raw reports for tools that consume those summaries.
    # Keep the artifact directory: runner-named reports can have the same filename
    # in different jobs and must never be flattened into one shared destination.
    $outputFile = Join-Path $outputRoot $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path $outputFile -Parent) | Out-Null
    $document.Save($outputFile)
}
Write-Host "Prepared $($reports.Count) Sonar import reports; excluded $removedPoints generated-code points."
