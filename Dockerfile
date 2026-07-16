# Stage 1: Build & Publish

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the .csproj first to take advantage of Docker layer caching
COPY cap_theorem_backend/cap_theorem_backend.csproj cap_theorem_backend/
RUN dotnet restore cap_theorem_backend/cap_theorem_backend.csproj

# Copy the rest of the source code
COPY cap_theorem_backend/ cap_theorem_backend/

RUN dotnet publish cap_theorem_backend/cap_theorem_backend.csproj \
    -c Release \
    -o /app/publish

# Stage 2: Runtime 

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "cap_theorem_backend.dll"]
