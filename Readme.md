# cde = Catalog Directory Entries

[TOC]

## Introduction
### What is cde
This is a utility to capture directory entries into catalog files for later processing and lookup. Processing includes capturing hashes, identifying duplicates and searching for file sy stem entries with assorted criteria.

It is written in C# and requires .Net 4.0.

It was inspired by and old and faithful utility called Cathy I have used in the past found at http://www.mtg.sk/rva/. One of the driving reasons to write cde was that Cathy is internally limited to 65535 directory entries.

#### cde
This is a command line utility to scan, find, hash, and perform duplicate file identification.

The executable `cde.exe` can be copied around by it self to be used anywhere that .Net 4.0 is available. The cde.config file is optional for cde.exe and it allows you to control some settings. [cde.config](#cde.config)

#### cdeWin
cdeWin is a Windows Forms application for searching, sorting and browsing of catalogs.

This application reads and writes a configuration file `cdeWinView.cfg`.
- This file is normally located in the Local Application Data folder of the current user.
- On standard Windows 7 machine its path would be
    - `C:\Users\username\AppData\Local\cde`
- If a file called cdeWinView.cfg exists in the current directory when cdeWin launches it will use the current directory for the configuration file and not the Local Application Data Folder.
- This file saves and restores information about cdeWin window and controls.
    - window location
    - search patterns
    - size of all the columns in list views
    - values of fields in the search parameters

The executable `cdeWin.exe` can be copied around by it self to be used anywhere that .Net 4.0 is available with the behavior of the cdeWinView.cfg file as described just above.

#### cdeWeb (unreleased)
This a web interface version of cde for searching but it is not released or finished.

It is built with ASP.Net, MVC, SignalR, Bootstrap and Angular.js.

### Contact Author
My name is Robin and you can contact me about cde using `rob at queenofblad.es`

This application will continue to evolve driven by my own and a few friends desires however the rate of change to date has not been very fast. It may evolve faster if there is sufficient interest from the public.

#### License Shareware Snowball
cde is currently released in Jan 2015 as a variant of **Shareware**, I would like to call **Shareware Snowball**.

If you like it enough I would appreciate a donation to cdeDonationAddress@gmail.com.

If you don't feel its reached your donation threshold then apply this rule.
1. Consider some other shareware software you like and have not donated for yet.
1. Add the perceived value of **cde** to this other shareware's value to you.
1. If this combination of value reaches your threshold for donation then donate to the other shareware software and consider yourself donated for cde as well.

I have no problem with individuals using this tool for themselves in a commercial environment and they can apply a donation threshold rule as they see fit.

If you are deploying this to other employees in a commercial environment for more than 6 weeks then I think you have allready passed a value threshold and should seriously consider that the application is of sufficient value for a donation based on the number of regular users of the software.

As a small incentive a 64 build of cde and cdeWin is available to people who donate for cde.
You woudl realy need to be dealing with a lot of file entries to need this. As the 32 bit version of cdeWin can deal with over 11 million file system entries using just over 1.5 GB of ram.

## Details

### Catalog files ".cde"
- cde creates a separate catalog file for each file system it scans.

- each catalog file ends with extension ".cde"
- each catalog file created has a file name derived from the drive letter, volume name and path or unc path to the file system.
     - These canonical names are not enforced, but are used when cde updates hashes on a file system that has an old catalog file available.
     - Renaming files is not a problem if you are not using hashes or don't care about hashes.
     - Duplicating a catalog to a different name will cause cde to load both original and duplicate as it does not check for duplicates catalog file contents.
- example catalog file names
    ```batch
    cde --scan c:\users\
    ```
    Produces a cde file name of `C-V3Win7-C__users.cde` on my machine.
    ```batch
    cde --scan \\unc\toothless\c$\users
    ```
    Produces a cde file name of `UNC-toothless_c__users_.cde`  on my home network.

- All catalog files in the current directory, or one directory below current directory will be loaded by cde or cdeWin when they are started.
    - The main reason for loading also from one directory down is to allow for file system permission to be applied to folders in the current directory to limit what catalog files are loaded by the current useres identity.

- Catalog files can contain MD5 Hashes for files as well if they have been added by using the "-hash" option.

- When hashing cde strives to only hash files that are of the same length as this means they could have the same content.

- When hashing cde tries to avoid work, by only hashing a fixed byte at the start of large files, to try and collect just enough information about a file to exclude it from being a duplicate of another file.
    - partial hashes are promoted to a hash of the entire file if another file is encountered with the same file length and a matching partial hash.

- When scanning a file system that you allready have a catalog file for, cde will scan the file system and then load the old catalog file looking for any hashes in the old catalog file that still match on file paths, file names and file sizes to copy to the newly updated catalog file. So the work of hashing a file system with unchanged files should not be lost.

### cde Operation

Some options only apply to some modes, in those cases the mode parameter must occur before the other parameters for the command line to be valid.

Valid:
```batch
cde -find afilename -path
```
Invalid:
```batch
cde -path -find afilename
```


### cde -scan Path
#### Valid Options for this mode
[`-minSize`](#parameter-options)
[`-maxSize`](#parameter-options)
[`-minDateTime`](#parameter-options)
[`-maxDateTime`](#parameter-options)
[`-minTime`](#parameter-options)
[`-maxTime`](#parameter-options)

This is the mode of operation that creates and updates catalog files.

When it creates new catalog files it will detect an old catalog file for the given scan target   and copy any Hash values from the old file to the new file for matching  file paths, file dates and sizes.

Only Last Modified Time of file system entries is captured into .cde files.

### cde -find String
#### Valid Options for this mode
[`-basePath`](#parameter-options)
[`-path`](#parameter-options)
[`-grep`](#parameter-options)
[`-repl`](#parameter-options)
[`-minSize`](#parameter-options)
[`-maxSize`](#parameter-options)
[`-minDateTime`](#parameter-options)
[`-maxDateTime`](#parameter-options)
[`-minTime`](#parameter-options)
[`-maxTime`](#parameter-options)

This is the mode for finding files in your catalogs using assorted filter criteria. If you wish to sort and browse it is suggested you use use cdeWin rather than cde.

### cde -hash
#### Valid Options for this mode
[`-basePath`](#parameter-options)
[`-minSize`](#parameter-options)
[`-maxSize`](#parameter-options)
[`-minDateTime`](#parameter-options)
[`-maxDateTime`](#parameter-options)
[`-minTime`](#parameter-options)
[`-maxTime`](#parameter-options)
[`-minHourAge`](#parameter-options)

This mode exists to provide file hashes to the Dupes mode.

It hashes files on different file systems in parallel. File systems are determined in a naive manner by a simple grouping of the root Volume or Share Name.

It will hash files in descending file size order.

If you have Hash running in Phase one and hit Ctrl-C it will actualy move from Phase one of hashing to Phase two where files are checked if they need to be promoted to Full Hash to determine uniqueness.
This second phase can also be interrupted with Ctrl-C.
By doing this you can  limit the hashing of  files to try and run Dupes on a subset of the larger files in the cataloged file system.

You can also use the option -minSize to limit the size of Hashed files and Dupes reported files

### cde -dupes
#### Valid Options for this mode
[`-basePath`](#parameter-options)
[`-minSize`](#parameter-options)
[`-maxSize`](#parameter-options)
[`-minDateTime`](#parameter-options)
[`-maxDateTime`](#parameter-options)
[`-minTime`](#parameter-options)
[`-maxTime`](#parameter-options)
[`-minHourAge`](#parameter-options)


This mode looks for duplicate files in all catalog files available.
It does no hashing itself and is very fast, however it does depend on hash being availabe in the catalog file and will not find any duplicated if no hashing has been done. So you will need to run Hash mode before using Dupes.

Dupes does not look at files on the file system at all, so if you run Dupes on a set of catlog files and it reports file duplicate and the file system has changed since the hashing of the catalogs was done you will not get useful results - Be Careful -

Consider using -minHourAge to limit Hash and Dupes work if your are cleanign up file systems.

### cde -dump
#### Valid Options for this mode
`No options supported.`

Output the full tree of file entries in the catologs in text format.

###  Parameter Options

| | Parameter&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; | Description |
| ---: | :--- | :--- |
| | `-basePath {Path}` | Defines the set of base paths to search, hash or find entries from, if not specified it defaults to the root of each catalog loaded. May take multiple parameters or be used multiple times as a parameter. If the path specified appears in more than one catalog file you will get results for every match.
| | `-path` | Include the path to a file when finding matching entry names.
| | `-grep` | The search parameter is treated as a regular expression for finding matches.
| | `-repl` | The first search will occur and the cde will prompte for subsequent searches when search complete.  An empty search exits the loop.
| | `-minSize {Size}` | Minimum size of file in bytes to include in processing.
| | `-maxSize {Size}` | Maximum size of file in bytes to include in processing.
| | `-minHours {Int}` | Minimum age of file in hours to include in processing.
| | `-minDate {DateTime}` | Minimum date and time inclusive to include in processing.
| | `-maxDate {DateTime}` | Maxnimum date and time inclusive to include in processing.
| | `-minTime {T}` | Minimum time inclusive to include in processing. This ignores the date part of file time.
| | `-maxTime {T}` | Maximum time inclusive to include in processing. This ignores the date part of file time.
| | `-maxResults {Int}` | Maximum number of results returned by cde.
| | `-exclude {Regex}` | A filter to exclude only entries that match these regexes for processing.
| | `-include {Regex}` | A filter to include only entries that match these Regexes for processing.

##### Date Time  Format for parameters
`<YYYY>-<MONTH>-<DD>T<HH>:<MM>:<SS>`

- Month field is an integer 1-12 or short Month Name
- There is no need to fill out all the fields of the date format to get a valid value.
- You may just specify from the beggining the parts you need.
- The `T` between date and time is required and must be upper case.

Valid Examples:
- "2014"
- "2014-12"
- "2014-Dec"
- "2014-Dec"
- "2014-12-02"
- "2014-12-02T"
- "2014-12-02T13"
- "2014-12-02T13:10"
- "2014-12-02T13:10:30"

##### Time Format for parameters
`<HH>:<MM>:<SS>`

- The time is 24 hour time format. 0 - 23
- There is no need to fill out all the fields of the time format to get a valid value.

Valid Examples:
- "9"
- "9:30"
- "9:30:15"

#####  Size format for parameters
The following suffixes case independent on size fields modify there value.

| Suffix | Description | Multiplier
| ---: | --- |
| KB | Kilobytes | 1000
| MB | Megabytes | 1000^2
| GB | Gigabytes | 1000^3

Suffixes must follow number with no spaces.

## Extra Details

### cde.config
There are 3 values that can be modified to change cde behaviour.

- `ProgressUpdateInterval`  which defaults to 5000.
    - the number of file entries scanned before a "." it output to console as a progress indicator.

- `Hash.FirstPassSizeInBytes` which defaults to 655356 bytes.
    - If you change cde.config after creating catalog files that contain collected hashes you need to recreate those hash files by deleting the catalog files, recreating with scan them and then hashing them again.

- `Hash.DegreesOfParallelism` which defaults to 2.
    - hashing will automatically try to do hashing on different file systems in parallel.
    - This setting defines how many concurrent processes will be hashing files on one file system.

###Examples

To create a cde catalog for C:\ drive.
```batch
cde --scan C:\
```

To find files containing word "system.dll" in catalogs in current directory.
```batch
cde --find system.dll
```

### Catalog file sizes and memory usage
These are examples and actual file sizes and memory footprints may vary depending
on the length of file and folder names.

| Count Files and Folders | Description | Memory at Launch | File Size
| ---: | --- | --- | --- |
| no files or folders | 32 bit cde | 18MB | 0
| 8,000 in one catalog | 32 bit cde | 19MB | 500KB
| 500,000,000 in one catalog  | 32 bit cde | 96MB | 22MB
| 1,500,000,000 in one catalog | 32 bit cde | 275MB | 65MB
| 2,000,000,000 in 3 catalogs | 32 bit cde | 360MB | 77 MB
| 11,000,000,000 in 7 catalogs | 32 bit cde | 1.52 GB | 500 MB
| 11,000,000,000 in 7 catalogs | 64 bit cde | 2.5 GB | 500 MB

The 64 or AnyCPU build on a 64 bit Windows uses a lot more memory than the 32 bit version.

The catalog files are not compressed internally as the use cases I have had till now makes them quicker without compression especially on SSD. They can be compressed quite well.

### Acknowledgements

The following sofwtare is used in the Development of this application.
- Microsofts Visual Studio
- protobuf-net https://code.google.com/p/protobuf-net/
- AlphaFS https://alphafs.codeplex.com/
- NUnit http://www.nunit.org/
- NDesk.Options http://www.ndesk.org/Options

The web version uses. (not available currently)
- AngularJS https://angularjs.org/
- Bootstrap http://getbootstrap.com/
- autofac http://autofac.org/
- SignalR http://signalr.net/
