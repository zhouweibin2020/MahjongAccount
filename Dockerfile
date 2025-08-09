FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
WORKDIR "/src/MahjongAccount"

FROM build AS publish
RUN dotnet publish "MahjongAccount.csproj"  -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV TZ=Asia/Shanghai
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone
ENV ASPNETCORE_URLS=http://+:80
RUN sed -i 's/TLSv1.2/TLSv1.0/g' /etc/ssl/openssl.cnf 2>/dev/null || true
RUN sed -i 's/DEFAULT@SECLEVEL=2/DEFAULT@SECLEVEL=1/g' /etc/ssl/openssl.cnf 2>/dev/null || true
ENTRYPOINT ["dotnet", "MahjongAccount.dll"]
