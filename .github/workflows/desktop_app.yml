name: Build Desktop App

on:
  push:
    branches: [ "main" ]

jobs:
  build:
    runs-on: windows-2019

    steps:
    - uses: actions/checkout@v4

    - name: Setup MSBuild
      uses: microsoft/setup-msbuild@v2

    - name: Setup NuGet
      uses: NuGet/setup-nuget@v2

    - name: Restore NuGet packages
      run: nuget restore ClientVisitManager.csproj

    - name: Build Application
      run: msbuild ClientVisitManager.csproj /p:Configuration=Release /p:OutputPath=bin\Release\

    - name: Upload Executable
      uses: actions/upload-artifact@v4
      with:
        name: ExonicSalonApp
        path: bin/Release/
