"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.parseCmds = exports.parsePaths = void 0;
/** @internal */
function parsePaths(str) {
    return str.split("\n");
}
exports.parsePaths = parsePaths;
/** @internal */
function parseCmds(str) {
    return str.split("\n");
}
exports.parseCmds = parseCmds;