# Kitsuneo

Monorepo do projeto Kitsuneo, contendo o frontend em Next.js e o backend em ASP.NET Core.

## Estrutura

```
Kitsuneo/
├── frontend/                       # Aplicação web (Next.js)
│   └── src/app/                    # App Router
└── backend/                        # API (ASP.NET Core)
    ├── Kitsuneo.slnx               # Solution
    └── src/
        ├── Kitsuneo.API/           # Camada de apresentação (controllers, configuração)
        ├── Kitsuneo.Application/   # Casos de uso e regras de aplicação
        ├── Kitsuneo.Core/          # Domínio (entidades, interfaces)
        └── Kitsuneo.Infrastructure/ # Acesso a dados e serviços externos
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
| Backend  | .NET 10, ASP.NET Core Web API, OpenAPI             |

## Pré-requisitos

- [Node.js](https://nodejs.org/) 20+
- [.NET SDK](https://dotnet.microsoft.com/download) 10

## Como executar

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Disponível em http://localhost:3000.

### Backend

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
| `dotnet watch --project src/Kitsuneo.API` | `backend` | Executa com hot reload |

## Convenção de commits

Este repositório segue o padrão [Conventional Commits](https://www.conventionalcommits.org/pt-br/):

```
<tipo>(<escopo opcional>): <descrição>
```

Tipos mais comuns: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`, `build`, `ci`.
Escopos sugeridos: `frontend`, `backend`.

Exemplo: `feat(backend): adiciona endpoint de autenticação`
