$folders = Get-ChildItem src\*\* | Where-Object { "bin","obj" -contains $_.Name }
$folders = $folders | select-object -first 11
#"trace1"
#$folders | % { $_.FullName }
#"trace2"
$folderSizes = $folders |
	select-object -Property FullName,@{n="Sum";e={
		(Get-ChildItem -recurse $_ |
			Where-Object { !$_.PsIsContainer }  |
			Measure-Object -Sum Length).Sum } }
$folderSizes
$folderSizes | foreach { Remove-Item -Force -Recurse $_.FullName } 
