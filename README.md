# Kitsuneo

Kitsuneo é um app para salvar e estudar frases em inglês e japonês, com leitura, forma falada e tradução.

Monorepo do projeto, contendo o frontend em Next.js e o backend em ASP.NET Core.

## Estrutura

```
Kitsuneo/
├── frontend/                       # Aplicação web (Next.js)
│   └── src/app/                    # App Router
└── backend/                        # API (ASP.NET Core)
    ├── Kitsuneo.slnx               # Solution
    ├── src/
    │   ├── Kitsuneo.API/           # Camada de apresentação (controllers, configuração)
    │   ├── Kitsuneo.Application/   # Casos de uso e regras de aplicação
    │   ├── Kitsuneo.Core/          # Domínio (entidades, interfaces)
    │   └── Kitsuneo.Infrastructure/ # Acesso a dados e serviços externos
    └── tests/
        └── Kitsuneo.Core.Tests/    # Testes unitários do domínio (xUnit + FluentAssertions)
```

### Dependências entre projetos do backend

```
API ──► Application ──► Core
 └────► Infrastructure ─┬► Application
                        └► Core
```

## Tecnologias

| Camada   | Stack                                              |
| -------- | -------------------------------------------------- |
| Frontend | Next.js 16, React 19, TypeScript, Tailwind CSS 4   |
| Backend  | .NET 10, ASP.NET Core Web API, OpenAPI, EF Core + PostgreSQL |
| Testes   | xUnit, FluentAssertions                            |

## Pré-requisitos

- [Node.js](https://nodejs.org/) 20+
- [.NET SDK](https://dotnet.microsoft.com/download) 10

## Configuração inicial

Na raiz do repositório, instale as dependências de tooling (Husky + commitlint):

```bash
npm install
```

O script `prepare` ativa automaticamente o hook `commit-msg`, que valida as mensagens de commit.

## Ambiente Docker

O `docker-compose.yml` sobe o PostgreSQL 18, o backend e o frontend.

| Serviço    | URL                                  |
| ---------- | ------------------------------------ |
| Frontend   | http://localhost:3000                |
| Backend    | http://localhost:8080                |
| Health     | http://localhost:8080/health         |
| OpenAPI    | http://localhost:8080/openapi/v1.json |
| PostgreSQL | `localhost:5432`                     |

### Segredos

Nenhum dado sensível fica versionado:

- **Senha do banco na API:** fica no [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) do .NET (`UserSecretsId: kitsuneo-api`), na chave `Database:Password`. O `appsettings.json` guarda apenas a connection string sem senha.
- **Senha do contêiner PostgreSQL:** fica no arquivo `.env` da raiz (ignorado pelo git), na variável `POSTGRES_PASSWORD`. Os dois valores devem ser iguais.

- **Chave do ElevenLabs (voz):** fica no user-secrets da API, na chave `ElevenLabs:ApiKey`. É opcional: sem ela, o resto da API funciona e só o endpoint de áudio responde `503`.

No Docker, o diretório do user-secrets do host é montado como somente leitura no contêiner do backend, que roda em `Development` para carregá-lo.

### Voz com IA (ElevenLabs)

`GET /api/v1/phrases/{id}/audio?slow=false` devolve a frase (o `SpokenForm`) lida em voz alta, em MP3. Assim a prática toca o ditado sem revelar o texto. Para a demonstração, usa o [plano gratuito do ElevenLabs](https://elevenlabs.io/pricing):

1. Crie uma conta em https://elevenlabs.io e gere uma chave em **Developers → API Keys** (basta a permissão *Text to Speech*).
2. Guarde a chave no user-secrets da API:

   ```bash
   dotnet user-secrets set "ElevenLabs:ApiKey" "<sua chave>" --project backend/src/Kitsuneo.API
   ```

O plano gratuito tem uma cota mensal pequena de créditos e só usa as vozes padrão pela API. Para economizar:

- O modelo padrão é o `eleven_flash_v2_5`, que fala inglês e japonês e custa metade dos créditos por caractere.
- Cada áudio gerado fica em cache no disco (`ElevenLabs:CacheDirectory`, por padrão `kitsuneo-audio` na pasta temporária), então tocar a mesma frase de novo não gasta créditos.
- `slow=true` gera uma versão mais lenta (velocidade 0,7), que é um áudio separado no cache.

Voz, modelo e demais opções podem ser trocados na seção `ElevenLabs` da configuração (por exemplo, `ElevenLabs:Voices:Japanese` para uma voz só para o japonês). O plano gratuito exige dar crédito ao ElevenLabs onde o áudio for usado, então a tela de prática deve exibir algo como "Voz gerada por ElevenLabs".

### Primeira execução

```bash
# 1. Variáveis do Docker Compose
cp .env.example .env
# edite o .env: defina POSTGRES_PASSWORD e, se necessário, HOST_UID/HOST_GID (id -u / id -g)

# 2. Mesma senha no user-secrets da API
dotnet user-secrets set "Database:Password" "<mesma senha do .env>" --project backend/src/Kitsuneo.API

# 3. Subir o ambiente
docker compose up -d --build
```

Comandos úteis:

```bash
docker compose logs -f backend   # logs de um serviço
docker compose up -d postgres    # apenas o banco (para rodar a API/frontend fora do Docker)
docker compose down              # para os contêineres
docker compose down -v           # para e apaga o volume do banco
```

## Como executar sem Docker

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Disponível em http://localhost:3000.

### Backend

Requer o PostgreSQL rodando (por exemplo, `docker compose up -d postgres`) e o user-secret `Database:Password` configurado.

```bash
cd backend
dotnet restore
dotnet run --project src/Kitsuneo.API
```

Disponível em http://localhost:5243 (ou https://localhost:7085 com o perfil `https`).
Em ambiente de desenvolvimento, o documento OpenAPI fica em `/openapi/v1.json`.

## Scripts úteis

| Comando                    | Diretório  | Descrição                   |
| -------------------------- | ---------- | --------------------------- |
| `npm run dev`              | `frontend` | Servidor de desenvolvimento |
| `npm run build`            | `frontend` | Build de produção           |
| `npm run lint`             | `frontend` | Lint com ESLint             |
| `dotnet build`             | `backend`  | Compila a solution          |
| `dotnet test`              | `backend`  | Executa os testes unitários |
| `dotnet watch --project src/Kitsuneo.API` | `backend` | Executa com hot reload |

## Convenção de commits

Este repositório segue o padrão [Conventional Commits](https://www.conventionalcommits.org/pt-br/):

```
<tipo>(<escopo opcional>): <descrição>
```

Tipos permitidos: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.
Escopos permitidos (opcionais): `frontend`, `backend`, `ci`, `deps`, `repo`.

As mensagens são validadas pelo [commitlint](https://commitlint.js.org/) localmente (hook do Husky) e no CI.
Para adicionar escopos, edite `commitlint.config.mjs`.

Exemplo: `feat(backend): adiciona endpoint de autenticação`
