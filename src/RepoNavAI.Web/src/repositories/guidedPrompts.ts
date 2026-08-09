export type GuidedPromptMode = 'Search' | 'Answer' | 'Orientation' | 'CodeFlow';

export interface RepositoryCapabilities {
  hasIndexedContent: boolean;
  hasSourceCode: boolean;
  hasTests: boolean;
  hasDocumentation: boolean;
  apiEndpoints: boolean;
  representativePaths: string[];
}

export interface GuidedPrompt {
  id: string;
  mode: GuidedPromptMode;
  text: string;
  requires?: keyof RepositoryCapabilities;
}

export const guidedPromptCatalog: readonly GuidedPrompt[] = [
  { id: 'entry-point', mode: 'Search', text: 'Where is the main application entry point and how is startup configured?', requires: 'hasSourceCode' },
  { id: 'architecture', mode: 'Answer', text: 'What are the main architectural components and how do they collaborate?', requires: 'hasSourceCode' },
  { id: 'orientation', mode: 'Orientation', text: 'Build me a practical plan for getting productive in this repository.', requires: 'hasSourceCode' },
  { id: 'core-flow', mode: 'CodeFlow', text: 'Trace a representative user request through the main application layers.', requires: 'apiEndpoints' },
  { id: 'change-impact', mode: 'Answer', text: 'Which parts of this repository are most risky to change, and why?', requires: 'hasSourceCode' },
  { id: 'tests', mode: 'Search', text: 'Where are the most important automated tests and what behavior do they protect?', requires: 'hasTests' },
  { id: 'api-auth', mode: 'CodeFlow', text: 'Trace an authenticated API request from its endpoint through authorization and persistence.', requires: 'apiEndpoints' },
  { id: 'api-catalog', mode: 'Search', text: 'Which API endpoints are the best starting points for understanding this application?', requires: 'apiEndpoints' }
] as const;

export function applicableGuidedPrompts(capabilities: RepositoryCapabilities): GuidedPrompt[] {
  if (!capabilities.hasIndexedContent) return [];
  const prompts = guidedPromptCatalog.filter(prompt => !prompt.requires || capabilities[prompt.requires]);
  if (!capabilities.hasSourceCode && capabilities.hasDocumentation) {
    const path = capabilities.representativePaths.find(value => value.toLowerCase().endsWith('.md'));
    if (path) prompts.push(
      { id: 'document-purpose', mode: 'Answer', text: `According to ${path}, what does this project do and how is it used?` },
      { id: 'document-source', mode: 'Search', text: path }
    );
  }
  return prompts;
}

export function nextGuidedPromptSet(prompts: readonly GuidedPrompt[], startIndex: number, count = 3): GuidedPrompt[] {
  if (prompts.length === 0 || count <= 0) return [];
  const size = Math.min(count, prompts.length);
  return Array.from({ length: size }, (_, offset) => prompts[(startIndex + offset) % prompts.length]!);
}
