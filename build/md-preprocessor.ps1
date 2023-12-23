function ExtractFileNamedFragment($file, $fragmentName) {
    $fragmentLines = New-Object System.Collections.Generic.List[object]
    $fileLines = Get-Content -Path $file
    $markerName = "doc:fragment:$fragmentName";
    $markerStarted = $false
    foreach ($fileLine in $fileLines) {
        if ($fileLine.Contains($markerName)) {
            $markerStarted = !$markerStarted;
        }
        else {
            if ($markerStarted) {
                $fragmentLines.Add($fileLine)
            }
        }
    }

    return $fragmentLines
}

function CountLeadingTabsOrSpaces($lines) {
    $count = -1;
    foreach ($line in $lines) {
        for ($i = 0; $i -lt $line.Length; $i++) {
            $c = $line[$i]            
            if (($c -ne "`.") -and ($c -ne " ")) {
                if (($count -eq -1) -or ($i -lt $count)) {
                    $count = $i
                }
                break;
            }
        }
    }
    if ($count -eq -1) {
        return 0;
    }
    return $count
}

function IdentFragment($lines) {
    $i = CountLeadingTabsOrSpaces $lines
    if ($i -gt 0) {
        return $lines | ForEach-Object { if ($_.Length -gt $i) { $_.Substring($i) } else { $_ } }
    }
    return $lines
}

function ProcessMarkdownFileIncludeLine($file, $line, $transformedLines) {
    $found = $line -match '.*\@\[\:cs\]\((.+)\,(.+)\).*'
    if (!$found) {
        throw "Cannot parse fragment include in file $file on line $line"
    }

    $fragmentFilePath = $matches[1]
    $fragmentName = $matches[2]

    $fileFolder = Split-Path -parent $file
    Write-Host "$fileFolder of $file"

    $fragmentLines = ExtractFileNamedFragment "$fileFolder/$fragmentFilePath" $fragmentName  
    if ($fragmentLines.Count -gt 0) {

        $fragmentLines = IdentFragment $fragmentLines

        $transformedLines.Add("``````cs")
        $transformedLines.AddRange($fragmentLines); 
        $transformedLines.Add("``````")
    }                
}

function ProcessMarkdown($file, $targetFile) {
    Write-Host "Processing $file into $targetFile ..."

    $templateLines = Get-Content -Path $file
 
    # Write-Host $templateLines[0]
    $transformedLines = New-Object System.Collections.Generic.List[object]
    foreach ($line in $templateLines) {
        if ($line.Contains("@[:cs](")) {
            ProcessMarkdownFileIncludeLine $file $line $transformedLines
        }
        else {
            $transformedLines.Add($line);
        }
    }

    Set-Content -Path $targetFile -Value ($transformedLines -join "`n") -NoNewline
}

# Find files ending in .t.md
$files = Get-ChildItem -Path '../' -Filter '*.t.md' -Recurse -ErrorAction SilentlyContinue | Select-Object -Property FullName -ExpandProperty FullName 

# For each found file run processing
foreach ($file in $files) {    
    $targetFile = $file.replace('.t.md', '.md');
    
    ProcessMarkdown $file $targetFile
}
