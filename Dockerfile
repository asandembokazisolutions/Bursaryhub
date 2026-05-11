# Stage 1: Build
FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0 AS build

WORKDIR /src

# Copy project file and restore
COPY Bursaryhub/*.csproj ./Bursaryhub/
RUN dotnet restore ./Bursaryhub/Bursaryhub.csproj

# Copy everything else
COPY . .

WORKDIR /src/Bursaryhub
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/nightly/aspnet:10.0

WORKDIR /app

RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Ensure app binds correctly inside container
ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80

ENTRYPOINT ["dotnet", "BursaryHub.dll"]
