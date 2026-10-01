# Hand analysis logs

Ordered markdown transcripts of simulated hands for checking pot math, side pots, stack deltas, and chip conservation.

Each log lists hands in **strict play order** (sequential hand number). For every hand you get:

- Hole cards and board (full visibility — AI-vs-AI simulation only)
- Action sequence
- Pot awards with winning hand category and per-seat payouts
- Per-seat stack changes (should sum to zero)
- Total chips before/after the hand (should match)

## Regenerate

From the repo root:

```bash
cd DotNet
dotnet run --project CinematicPoker.Simulation -- \
  1000 42 60 ../docs/hand-analysis/hands-seed42-1000.md
```

Arguments: `hands`, `seed`, `equityIterations`, optional `markdownLogPath`.

Smaller sample (faster to skim):

```bash
dotnet run --project CinematicPoker.Simulation -- \
  100 42 60 ../docs/hand-analysis/hands-seed42-100.md
```

The simulation must pass all invariants before writing the file. If a hand fails conservation checks, the run aborts and no log is updated.

## Committed files

| File | Hands | Seed | Notes |
| --- | ---: | ---: | --- |
| [hands-seed42-100.md](./hands-seed42-100.md) | 100 | 42 | Quick manual review |
| [hands-seed42-1000.md](./hands-seed42-1000.md) | 1000 | 42 | Deeper spot checks |

### Download PDF

Direct download (same content as the markdown logs, formatted for printing):

| PDF | Hands | Download |
| --- | ---: | --- |
| **100-hand sample** | 100 | [**hands-seed42-100.pdf**](https://github.com/Simmo76/red-dead-texas-holdem/raw/main/docs/hand-analysis/hands-seed42-100.pdf) |
| **Full 1000-hand log** | 1000 | [**hands-seed42-1000.pdf**](https://github.com/Simmo76/red-dead-texas-holdem/raw/main/docs/hand-analysis/hands-seed42-1000.pdf) |

Until this branch is merged to `main`, use the branch URL instead, e.g.  
`https://github.com/Simmo76/red-dead-texas-holdem/raw/cursor/hand-analysis-logs-96cd/docs/hand-analysis/hands-seed42-100.pdf`

Regenerate PDFs after updating markdown:

```bash
./docs/hand-analysis/generate-pdf.sh
```

Use a different seed or hand count when investigating a specific failure; keep the same seed to diff logs across engine versions.
