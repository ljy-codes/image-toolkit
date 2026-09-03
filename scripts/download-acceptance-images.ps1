param(
    [string]$OutputDirectory = "tmp/acceptance-100",
    [int]$TargetCount = 100,
    [int]$RequestDelayMilliseconds = 1500,
    [string[]]$SceneKeys = @()
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine($PSScriptRoot, "..", $OutputDirectory))
$imageDirectory = Join-Path $root "source"
New-Item -ItemType Directory -Path $imageDirectory -Force | Out-Null

$scenes = [ordered]@{
    "portrait" = "portrait person photography"
    "group" = "group of people photography"
    "product" = "product isolated object photography"
    "glass" = "transparent glass object photography"
    "animal" = "animal fur close up photography"
    "insect" = "insect macro photography"
    "fine-structure" = "bicycle wheel spokes photography"
    "plant" = "green plant leaf photography"
    "food" = "food dish photography"
    "architecture" = "architecture building photography"
    "low-light" = "night low light photography"
    "white-object" = "white object white background photography"
    "vehicle" = "vehicle street photography"
    "landscape" = "landscape nature photography"
    "textile" = "textile fabric close up photography"
    "electronics" = "electronic device product photography"
}
if ($SceneKeys.Count -gt 0) {
    $selectedScenes = [ordered]@{}
    foreach ($sceneKey in $SceneKeys) {
        if (-not $scenes.Contains($sceneKey)) {
            throw "未知场景：$sceneKey"
        }

        $selectedScenes[$sceneKey] = $scenes[$sceneKey]
    }

    $scenes = $selectedScenes
}

$headers = @{
    "User-Agent" =
        "SuyingshuAcceptance/1.0 " +
        "(https://github.com/ljy-codes/image-toolkit; local quality verification)"
}
$seenPages = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$records = [System.Collections.Generic.List[object]]::new()
$sequence = 0
$perSceneTarget = [Math]::Ceiling($TargetCount / $scenes.Count)

foreach ($scene in $scenes.GetEnumerator()) {
    if ($records.Count -ge $TargetCount) {
        break
    }

    $query = [Uri]::EscapeDataString($scene.Value)
    $api =
        "https://commons.wikimedia.org/w/api.php?action=query" +
        "&generator=search&gsrsearch=$query&gsrnamespace=6&gsrlimit=35" +
        "&prop=imageinfo&iiprop=url%7Csize%7Cextmetadata&iiurlwidth=1200" +
        "&format=json&formatversion=2"
    $response = Invoke-RestMethod -Uri $api -Headers $headers
    Start-Sleep -Milliseconds $RequestDelayMilliseconds
    $sceneCount = 0

    foreach ($page in $response.query.pages) {
        if ($records.Count -ge $TargetCount -or $sceneCount -ge $perSceneTarget) {
            break
        }

        if (-not $seenPages.Add([string]$page.pageid)) {
            continue
        }

        $extension = [System.IO.Path]::GetExtension($page.title).ToLowerInvariant()
        if ($extension -notin @(".jpg", ".jpeg", ".png", ".webp")) {
            continue
        }

        $info = $page.imageinfo[0]
        $downloadUrl = if ($info.thumburl) { $info.thumburl } else { $info.url }
        $sequence++
        $normalizedExtension = if ($extension -eq ".jpeg") { ".jpg" } else { $extension }
        $fileName = "{0:D3}-{1}{2}" -f $sequence, $scene.Key, $normalizedExtension
        $targetPath = Join-Path $imageDirectory $fileName

        $downloaded = $false
        for ($attempt = 1; $attempt -le 4 -and -not $downloaded; $attempt++) {
            try {
                Invoke-WebRequest -Uri $downloadUrl -Headers $headers -OutFile $targetPath
                $downloaded = (Get-Item -LiteralPath $targetPath).Length -gt 0
                Start-Sleep -Milliseconds $RequestDelayMilliseconds
            }
            catch {
                Remove-Item -LiteralPath $targetPath -Force -ErrorAction SilentlyContinue
                if ($attempt -eq 4) {
                    Write-Warning "下载失败：$($page.title) - $($_.Exception.Message)"
                }
                else {
                    Start-Sleep -Seconds (15 * $attempt)
                }
            }
        }

        if (-not $downloaded) {
            $sequence--
            continue
        }

        $metadata = $info.extmetadata
        $records.Add([pscustomobject]@{
            Sequence = $sequence
            Scene = $scene.Key
            FileName = $fileName
            SourceTitle = $page.title
            PageUrl = $info.descriptionurl
            DownloadUrl = $downloadUrl
            License = $metadata.LicenseShortName.value
            Artist = ($metadata.Artist.value -replace "<[^>]+>", "")
            OriginalWidth = $info.width
            OriginalHeight = $info.height
            Bytes = (Get-Item -LiteralPath $targetPath).Length
            Sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
        })
        $sceneCount++
    }
}

$records |
    Export-Csv -LiteralPath (Join-Path $root "sources.csv") `
        -NoTypeInformation `
        -Encoding utf8BOM
$records |
    ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $root "sources.json") -Encoding utf8

if ($records.Count -lt $TargetCount) {
    throw "只下载到 $($records.Count) 张图片，未达到目标 $TargetCount 张。"
}

Write-Host "已下载 $($records.Count) 张验收图片到 $imageDirectory"
