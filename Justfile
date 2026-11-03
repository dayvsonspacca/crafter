run:
    dotnet run --project apps/desktop

build:
    dotnet build

test:
    dotnet test

publish:
    dotnet publish apps/desktop -c Release -r linux-x64 -o dist
