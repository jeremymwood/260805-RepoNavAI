import { describe, expect, it } from 'vitest';
import { connectedNodeIds, filterArchitectureNodes, type ArchitectureGraph, type ArchitectureNode } from './RepositoryArchitectureMap';

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
  it('searches architecture roles', () => {
    expect(filterArchitectureNodes([{ ...nodes[1]!, role: 'Controller' }], 'All', 'controller')).toHaveLength(1);
  });
  it('focuses on a node and its immediate relationships', () => {
    const graph: ArchitectureGraph = { schemaVersion: '1.1', commitSha: 'abc', isTruncated: false, totalNodeCount: 3, nodes, edges: [
      { id: 'one', sourceId: 'module:src', targetId: 'file:api', kind: 'Contains', label: 'contains', evidenceLevel: 'Confirmed' },
      { id: 'two', sourceId: 'file:api', targetId: 'endpoint:get', kind: 'Declares', label: 'declares', evidenceLevel: 'Confirmed' }
    ] };
    expect([...connectedNodeIds(graph, 'file:api')]).toEqual(['file:api', 'module:src', 'endpoint:get']);
    expect([...connectedNodeIds(graph, 'module:src')]).toEqual(['module:src', 'file:api']);
  });
});
