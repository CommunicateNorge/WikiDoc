Param
(   
    # Displays the current time, but does not set it.
    [Parameter(Mandatory=$True)]
    [alias("source")]
    [string]$SourceFolder,

    [Parameter(Mandatory=$True)]
    [alias("bld")]
    [string]$BuildNumber,

    [Parameter(Mandatory=$True)]
    [alias("n")]
    [string]$Name,

    [Parameter(Mandatory=$True)]
    [alias("env")]
    [string]$Environment,

    [Parameter(Mandatory=$False)]
    [string]$BuildID,

    [Parameter(Mandatory=$False)]
    [string]$QueuedBy,

    [Parameter(Mandatory=$False)]
    [string]$SourceBranch,

	[Parameter(Mandatory=$False)]
    [string]$TargetRoot = "ftp://ftp01.communicate.no/mwl/Drop",

	[Parameter(Mandatory=$False)]
    [string]$FtpUser = "customers",

	[Parameter(Mandatory=$False)]
    [string]$FtpPwd = "Cu5Tom3r5",

    # Displays help.
    [alias("h")]
    [switch]$help
)

Write-Host "Environment $Environment";
Write-Host "Name $Name";
Write-Host "Build number $BuildNumber";
Write-Host "Build ID $BuildID";
Write-Host "Queued by $QueuedBy";
Write-Host "Source branch $SourceBranch";

$TargetFolder = "$($Environment)-$($Name)_$($BuildNumber)";

Write-Host "Copying files from source folder: $SourceFolder"
Write-Host "Copying files to target folder: $TargetRoot/$TargetFolder"

try
{
    $ftprequest = [System.Net.FtpWebRequest]::Create("$TargetRoot/$TargetFolder");
    $ftprequest.Method = [System.Net.WebRequestMethods+Ftp]::MakeDirectory
    $ftprequest.UseBinary = $true
    $ftprequest.Credentials = new-object System.Net.NetworkCredential("$FtpUser", "$FtpPwd")

    $response = $ftprequest.GetResponse();

    Write-Host Upload File Complete, status $response.StatusDescription

    $response.Close();
}
catch
{

}

ls $SourceFolder -Recurse | % { 

    $file = $_;
    $fpath = $file.FullName.Replace("$SourceFolder", "$TargetRoot/$TargetFolder");
    $fpath = $fpath.Replace("\","/");
    echo $fpath

    if(-not $_.PSIsContainer)
    {
		Write-Host "Uploading file: $fpath"
        # create the FtpWebRequest and configure it
        $ftp = [System.Net.FtpWebRequest]::Create($fpath)
        $ftp = [System.Net.FtpWebRequest]$ftp
        $ftp.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
        $ftp.Credentials = new-object System.Net.NetworkCredential("$FtpUser", "$FtpPwd")
        $ftp.UseBinary = $true
        $ftp.UsePassive = $true

        # read in the file to upload as a byte array
        $content = [System.IO.File]::ReadAllBytes($file.FullName)
        $ftp.ContentLength = $content.Length
        # get the request stream, and write the bytes into it
        $rs = $ftp.GetRequestStream()
        $rs.Write($content, 0, $content.Length)
        # be sure to clean up after ourselves
        $rs.Close()
        $rs.Dispose()
    }
    else
    {
        try
        {
			Write-Host "Creating folder: $fpath"
            $ftprequest = [System.Net.FtpWebRequest]::Create($fpath);
            $ftprequest.Method = [System.Net.WebRequestMethods+Ftp]::MakeDirectory
            $ftprequest.UseBinary = $true
            $ftprequest.Credentials = new-object System.Net.NetworkCredential("$FtpUser", "$FtpPwd")

            $response = $ftprequest.GetResponse();

            Write-Host Upload File Complete, status $response.StatusDescription

            $response.Close();
        }
        catch
        {

        }
    }
}

Write-Host "Done!"