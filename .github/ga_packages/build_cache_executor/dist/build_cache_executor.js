"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    Object.defineProperty(o, k2, { enumerable: true, get: function() { return m[k]; } });
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || function (mod) {
    if (mod && mod.__esModule) return mod;
    var result = {};
    if (mod != null) for (var k in mod) if (k !== "default" && Object.prototype.hasOwnProperty.call(mod, k)) __createBinding(result, mod, k);
    __setModuleDefault(result, mod);
    return result;
};
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.actionCore = void 0;
const cache = __importStar(require("@actions/cache"));
const exec = __importStar(require("@actions/exec"));
const utils = __importStar(require("./utils/common"));
const input = __importStar(require("./utils/input_parsing"));
/**
 * Builds and caches the resulting artifacts. In order to store the artifacts in a cache, an hash is calculated over paths and the result is used as key in the dictionary of the cache.
 * The function can throw exceptions.
 * @param paths New line separated paths that need to be cached after the build (same paths used to create a hash)
 * @param cmds New line separated  cmds to build
 * @param oss Where to print the output messages
 * @param hashPrefix Prefix added in front of the hash that is going to be used as key in the cache dictionary
 * @param hashOptions Extra options for the default hash function
 * @param hashFunc Custom hash function if the default doesn't fullfil the user's needs
 * @returns CacheKey necessary to recover the cached build later on. If the function fails, undefined is returned together with an Error explaining the reason.
 */
function actionCore(paths, cmds, oss, hashPrefix, hashOptions, hashFunc) {
    return __awaiter(this, void 0, void 0, function* () {
        if (cmds.length === 0 || paths.length === 0) {
            throw new Error(`No commands were supplied, nothing to do.`);
        }
        const parsedPaths = input.parsePaths(paths);
        const parsedCmds = input.parseCmds(cmds);
        let hash;
        try {
            hash =
                hashFunc !== undefined
                    ? yield hashFunc(parsedPaths, oss, hashPrefix)
                    : yield utils.tryGetHash(parsedPaths, oss, hashPrefix);
        }
        catch (err) {
            throw new Error(`While calculating the hash something went terribly wrong: ${err.message}`);
        }
        let cacheKey = undefined;
        if (hash !== undefined) {
            oss.info(`Hash key for ${parsedPaths.join("\n")} is: ${hash}`);
            try {
                cacheKey = yield cache.restoreCache(parsedPaths, hash);
            }
            catch (err) {
                oss.error(`Impossible to retrieve cache: ${err}\n The build will start momentarily...`);
            }
        }
        else {
            throw new Error(`No hash could be calculated, so nothing to search in cache. Since what's going to be built now can't be cached, abort!`);
        }
        if (cacheKey === undefined) {
            oss.info(`No cache was found, so the command will be executed...`);
            try {
                for (const cmd of parsedCmds) {
                    const returnCode = yield exec.exec(cmd);
                    if (returnCode !== 0) {
                        throw Error(`Executing a command ${cmd} failed with code ${returnCode}. Stopping execution!`);
                    }
                }
            }
            catch (err) {
                throw new Error(`Something went terribly wrong while executing a shell command: ${err.message}`);
            }
            if (hash !== undefined) {
                try {
                    const cacheId = yield cache.saveCache(parsedPaths, hash);
                    oss.info(`Cache properly created with id ${cacheId}`);
                }
                catch (error) {
                    throw new Error(`The cache could not be saved because ${error.message}`);
                }
            }
        }
        else {
            oss.info(`A build was found in cache with cacheKey: ${cacheKey}\nskipping building...`);
        }
        return cacheKey !== null && cacheKey !== void 0 ? cacheKey : "no hash calculated";
    });
}
exports.actionCore = actionCore;