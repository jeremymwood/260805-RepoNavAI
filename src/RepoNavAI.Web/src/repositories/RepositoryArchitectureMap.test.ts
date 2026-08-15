import { describe, expect, it } from 'vitest';
import { filterArchitectureNodes, type ArchitectureNode } from './RepositoryArchitectureMap';

const nodes: ArchitectureNode[] = [
  { id: 'module:src', label: 'src', kind: 'Module', childCount: 2 },
  { id: 'file:api', label: 'OrdersController.cs', kind: 'File', path: 'src/Api/OrdersController.cs', language: 'csharp', childCount: 0 },
  { id: 'endpoint:get', label: 'GET /orders', kind: 'Endpoint', path: 'src/Api/OrdersController.cs', childCount: 0 }
];

describe('architecture map filtering', () => {
  it('filters by component type', () => expect(filterArchitectureNodes(nodes, 'Endpoint', '')).toEqual([nodes[2]]));
  it('searches labels, paths, and languages without case sensitivity', () => {
    expect(filterArchitectureNodes(nodes, 'All', 'orderscontroller')).toEqual([nodes[1], nodes[2]]);
    expect(filterArchitectureNodes(nodes, 'All', 'CSHARP')).toEqual([nodes[1]]);
  });
});
