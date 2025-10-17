# Vertrau OpenTelemetry Sample

Este repositório contém uma aplicação ASP.NET Core minimalista criada pela Vertrau para demonstrar, passo a passo, como os serviços da empresa se integram ao ecossistema do [OpenTelemetry](https://opentelemetry.io/). O objetivo principal é oferecer um material de referência para outras equipes entenderem como habilitar observabilidade completa — métricas, traces e logs — em aplicações .NET seguindo o padrão adotado internamente.

## Por que esta aplicação existe?

Na Vertrau, todas as aplicações de produção precisam expor dados de observabilidade de forma consistente para permitir diagnósticos rápidos, correlação entre serviços e monitoramento contínuo. Este projeto exemplifica a configuração base recomendada pela empresa, incluindo propagação de contexto, exportação de dados via OTLP e integração com Prometheus e Serilog. Ao estudar o código e executar o serviço, você verá exatamente quais bibliotecas usamos, como organizamos os registradores e como publicamos endpoints de saúde e métricas.

## Como a aplicação funciona

1. **Inicialização e configuração**  
   * Define variáveis importantes, como o `serverUrl` (`http://localhost:12345`), o nome lógico do serviço (`app-test`) e a URL do coletor OTLP (`http://localhost:4317`).  
   * Lê variáveis de ambiente para permitir ajustes sem recompilação.

2. **Propagação de contexto**  
   * Configura um `CompositeTextMapPropagator` com `TraceContextPropagator` e `BaggagePropagator`, garantindo que headers de trace distribuído sejam lidos e escritos em todas as requisições.

3. **Logging estruturado**  
   * Utiliza Serilog com `Serilog.Enrichers.Span` para incluir IDs de trace, span e baggage nos logs.  
   * Habilita `UseSerilogRequestLogging` para registrar automaticamente cada requisição HTTP.

4. **OpenTelemetry**  
   * Ativa `services.AddOpenTelemetry()` e registra:
     * **Métricas**: instrumentação de ASP.NET Core, HttpClient e runtime, além de exportação para Prometheus (`/metrics`).  
     * **Traces**: instrumentação de ASP.NET Core e HttpClient, com exportação OTLP para o coletor configurado.  
     * **Logs**: exportação de logs para console por meio do provedor OpenTelemetry.

5. **Health Checks e métricas HTTP**  
   * Registra health checks com `ForwardToPrometheus` para que o status seja exposto como métrica.  
   * Usa `app.MapHealthChecks` em `/readiness` (somente o check "self") e `/healthcheck` (todos os checks) com resposta formatada para o HealthChecks UI.  
   * Ativa `UseHttpMetrics` para coletar métricas de latência e contadores HTTP automaticamente.

6. **Rotas expostas**  
   * `GET /` retorna `"hello world"`, funcionando como prova de vida para os traces.  
   * `GET /readiness` e `GET /healthcheck` expõem o estado da aplicação.  
   * `GET /metrics` entrega as métricas no formato Prometheus.

## Como executar localmente

1. **Pré-requisitos**
   * .NET 8 SDK instalado.
   * Um coletor OpenTelemetry ou outro backend OTLP escutando em `http://localhost:4317` (opcional, mas necessário para visualizar traces fora do console).
   * Para métricas, recomenda-se executar um servidor Prometheus apontando para `http://localhost:12345/metrics`.

2. **Passos**
   ```bash
   dotnet restore
   dotnet run
   ```
   A aplicação ficará disponível em `http://localhost:12345`.

3. **Verificando observabilidade**
   * Acesse `http://localhost:12345/` para gerar uma requisição e observar o log no console.  
   * Consulte `http://localhost:12345/metrics` para visualizar as métricas expostas.  
   * Consulte `http://localhost:12345/readiness` e `http://localhost:12345/healthcheck` para checar o estado do serviço.  
   * Envie requisições HTTP para outro serviço para ver a instrumentação `HttpClient` (adapte conforme necessário).

## Estrutura principal

```
Program.cs         // Toda a configuração de OpenTelemetry, health checks, logging e endpoints
appsettings.json  // Configuração adicional padrão do ASP.NET Core
```

Ao replicar esta estrutura em outros projetos, basta ajustar os identificadores do serviço (nome, versão, endpoint OTLP) e incluir instrumentações adicionais relevantes ao seu contexto. O restante da configuração já segue o padrão validado pela Vertrau.

## Próximos passos sugeridos

* Integrar com o coletor padrão da Vertrau (Helm chart ou docker-compose interno).  
* Publicar dashboards de exemplo no Grafana para facilitar onboarding.  
* Adicionar exemplos de instrumentação manual (`ActivitySource`) quando houver regras de negócio críticas.

Esperamos que este exemplo sirva como um guia claro para equipes que precisam conectar suas aplicações Vertrau ao OpenTelemetry com segurança e consistência.
