run:
    dotnet run --project src/Crafter.Desktop

build:
    dotnet build

test:
    dotnet test

publish:
    dotnet publish src/Crafter.Desktop -c Release -r linux-x64 -o dist
