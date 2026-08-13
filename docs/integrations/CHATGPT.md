# ChatGPT Integration

**Status:** separate deployment/integration profile; not the 0.1 local-stdio reference path

Hero Passport 0.1 is designed around a local coding-agent host that can launch `hero-passport mcp` as stdio beside the project.

ChatGPT product surfaces may expose Skills/plugins/apps/MCP through product-managed mechanisms rather than arbitrary local-project stdio. Current OpenAI documentation states that Skills follow the open Agent Skills standard and that plugins can package workflow Skills with MCP/app capabilities.

Therefore:

- keep `skills/hero-passport/` portable;
- do not make ChatGPT-specific APIs a Core dependency;
- do not expose a public Hero Passport endpoint merely to claim ChatGPT support;
- only document a ChatGPT deployment after the current official OpenAI mechanism has been threat-modeled and tested.

Possible future paths (each requires current official verification) include a private supported MCP reachability mechanism or a hosted/plugin distribution profile. Those paths must preserve the same HP-MCP semantics and privacy model, while adding whatever authorization/project-binding design remote access requires.

Until that work exists, ChatGPT is not a 0.1 Qualified local-project target.
